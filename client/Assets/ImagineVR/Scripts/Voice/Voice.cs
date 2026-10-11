using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ImagineVR
{
    /// <summary>
    /// Push-to-talk. While the talk button is held the microphone records; on release the
    /// recording is sent as one request. A press shorter than 0.4 seconds is ignored.
    /// </summary>
    public sealed class Voice : MonoBehaviour
    {
        public Rig Rig;
        public Terminal Terminal;
        public Conductor Conductor;

        const int SampleRate = 16000;
        const int MaxSeconds = 30;
        const float MinSeconds = 0.4f;

        AudioClip _clip;
        string _device;
        bool _recording;
        float _startedAt;
        TerminalState _stateBefore;
        readonly float[] _meter = new float[512];
        bool _warned;
        float _peak;   // loudest level heard in this recording: 0 means the microphone gave silence

        public bool HasMicrophone => Microphone.devices != null && Microphone.devices.Length > 0;

        void Start()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) Permission.RequestUserPermission(Permission.Microphone);
#endif
        }

        void Update()
        {
            if (Rig == null) return;
            if (Rig.TalkPressed && !_recording) Begin();
            if (_recording)
            {
                Terminal.SetMic(Level());
                if (Rig.TalkReleased || !Rig.TalkHeld || Time.time - _startedAt > MaxSeconds - 0.5f) End();
            }
        }

        void Begin()
        {
            _peak = 0f;
            if (!HasMicrophone)
            {
                SessionLog.Write("voice", "what", "no microphone");
                if (!_warned)
                {
                    _warned = true;
                    Terminal.Note("no microphone found. Allow microphone access, or type a request instead.");
                }
                return;
            }
            _device = Microphone.devices[0];
            _clip = Microphone.Start(_device, false, MaxSeconds, SampleRate);
            if (_clip == null) return;
            _recording = true;
            _startedAt = Time.time;
            SessionLog.Write("voice", "what", "listening", "microphone", _device);
            _stateBefore = Terminal.State == TerminalState.Listening ? TerminalState.Ready : Terminal.State;
            Terminal.SetState(TerminalState.Listening);
            Terminal.SetHint("release to send");
            Sfx.Play("listen_on", Terminal.transform.position, 0.5f);
            Rig.Haptic(true, 0.4f, 0.04f);
        }

        void End()
        {
            _recording = false;
            int position = Microphone.GetPosition(_device);
            Microphone.End(_device);
            float seconds = Time.time - _startedAt;
            Terminal.SetMic(0f);
            Terminal.SetHint(HintText());
            Sfx.Play("listen_off", Terminal.transform.position, 0.45f);

            if (_clip == null || seconds < MinSeconds || position <= 0)
            {
                SessionLog.Write("voice", "what", "dropped", "seconds", seconds, "samples", position,
                    "why", seconds < MinSeconds ? "too short" : "nothing recorded");
                Terminal.SetState(_stateBefore == TerminalState.Problem ? TerminalState.Ready : _stateBefore);
                return;
            }
            SessionLog.Write("voice", "what", "sent", "seconds", seconds, "samples", position, "rate", _clip.frequency, "loudest", _peak);
            var samples = new float[Mathf.Min(position, _clip.samples) * _clip.channels];
            _clip.GetData(samples, 0);
            // the server takes 16 kHz mono only; some microphones ignore the rate they were asked for
            byte[] wav = EncodeWav(ToMono16k(samples, _clip.channels, _clip.frequency), 1, SampleRate);
            Terminal.SetState(TerminalState.Thinking);   // acknowledge straight away
            Conductor.SubmitAudio(wav);
        }

        /// <summary>Stops listening and throws the recording away.</summary>
        public void Cancel()
        {
            if (!_recording) return;
            _recording = false;
            Microphone.End(_device);
            Terminal.SetMic(0f);
            Terminal.SetHint(HintText());
            SessionLog.Write("voice", "what", "cancelled");
        }

        /// <summary>Something to say next, shown beside the hint. The app keeps it fresh (see ImagineApp.NextIdea).</summary>
        public string Idea;

        public string HintText()
        {
            string how = Rig != null && Rig.XrActive ? "hold A to speak" : "hold Space to speak, or type";
            // the hint row is narrow, and the bubble by the hand already says how to talk
            return string.IsNullOrEmpty(Idea) ? how : "try \"" + Idea + "\"";
        }

        /// <summary>Loudness of the last few milliseconds, 0..1, for the terminal's meter.</summary>
        float Level()
        {
            if (_clip == null) return 0f;
            int position = Microphone.GetPosition(_device);
            int start = position - _meter.Length;
            if (start < 0) return 0f;
            _clip.GetData(_meter, start);
            float sum = 0f;
            for (int i = 0; i < _meter.Length; i++) sum += _meter[i] * _meter[i];
            float rms = Mathf.Sqrt(sum / _meter.Length);
            _peak = Mathf.Max(_peak, Mathf.Clamp01(rms * 9f));
            return Mathf.Clamp01(rms * 9f);
        }

        /// <summary>Mixes down to mono and, if the microphone recorded at another rate, resamples to 16 kHz.</summary>
        public static float[] ToMono16k(float[] samples, int channels, int rate)
        {
            channels = Mathf.Max(1, channels);
            int frames = samples.Length / channels;
            var mono = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float v = 0f;
                for (int c = 0; c < channels; c++) v += samples[f * channels + c];
                mono[f] = v / channels;
            }
            if (rate == SampleRate || rate <= 0 || frames < 2) return mono;
            int outFrames = Mathf.Max(1, (int)((long)frames * SampleRate / rate));
            var resampled = new float[outFrames];
            float step = (float)rate / SampleRate;
            // average the input each output sample covers when slowing down, so high notes do not fold over
            int span = Mathf.Max(1, Mathf.RoundToInt(step));
            for (int i = 0; i < outFrames; i++)
            {
                float at = i * step;
                int a = Mathf.Min((int)at, frames - 1);
                if (span <= 1)
                {
                    int b = Mathf.Min(a + 1, frames - 1);
                    resampled[i] = Mathf.Lerp(mono[a], mono[b], at - a);
                }
                else
                {
                    float sum = 0f;
                    int n = 0;
                    for (int k = a; k < a + span && k < frames; k++, n++) sum += mono[k];
                    resampled[i] = sum / Mathf.Max(1, n);
                }
            }
            return resampled;
        }

        /// <summary>16-bit PCM WAV, mixed down to mono.</summary>
        public static byte[] EncodeWav(float[] samples, int channels, int rate)
        {
            int frames = samples.Length / Mathf.Max(1, channels);
            int dataBytes = frames * 2;
            var wav = new byte[44 + dataBytes];
            void Ascii(int offset, string text) { for (int i = 0; i < text.Length; i++) wav[offset + i] = (byte)text[i]; }
            void Int32(int offset, int value) { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, wav, offset, 4); }
            void Int16(int offset, short value) { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, wav, offset, 2); }
            Ascii(0, "RIFF"); Int32(4, 36 + dataBytes); Ascii(8, "WAVE");
            Ascii(12, "fmt "); Int32(16, 16); Int16(20, 1); Int16(22, 1); Int32(24, rate); Int32(28, rate * 2); Int16(32, 2); Int16(34, 16);
            Ascii(36, "data"); Int32(40, dataBytes);
            for (int f = 0; f < frames; f++)
            {
                float v = 0f;
                for (int c = 0; c < channels; c++) v += samples[f * channels + c];
                v = Mathf.Clamp(v / channels, -1f, 1f);
                short s = (short)(v * 32767f);
                wav[44 + f * 2] = (byte)(s & 0xFF);
                wav[45 + f * 2] = (byte)((s >> 8) & 0xFF);
            }
            return wav;
        }
    }
}
