# Relax generation timeouts

Increase backend first-text allowance from 9 to 30 seconds and content-stall allowance from 8 to 15 seconds. Preserve environment overrides, model selection, the single retry and fallback behavior. Keep server heartbeats. Increase Unity connection-idle allowance from 8 to 15 seconds and total turn allowance from 45 to 90 seconds in both code defaults and the Resources settings asset, so a first-token retry is not cut off by the client.

This changes maximum waiting tolerance, not normal response latency. Existing acknowledgement and streamed status remain unchanged. Test with simulated time rather than paid provider requests or long sleeps. No merge, push or deployment is included.

Deployment note: backend defaults apply after deploying/restarting the service; any existing IMAGINE_FIRST_TOKEN_SECONDS or IMAGINE_STALL_SECONDS environment values still take precedence. The Unity settings require rebuilding the client. Neither step was performed by this change.
