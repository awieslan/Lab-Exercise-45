# HTTP Smart Controller XR

WebXR **AR** version of the Fish Click controller. Same functionality as [HTTP-IoT-LAN-Website-Controller](../HTTP-IoT-LAN-Website-Controller): tap the fish to change the color and send it to the Unity server.

## Differences from the web version

| Web version | WebXR AR version |
|-------------|------------------|
| Click fish with mouse | Tap fish with finger (AR) or pointer (desktop) |
| 2D rotating fish SVG | 3D fish on the ground in AR |
| Same Unity server (`:8080`) | Same Unity server (`:8080`) |
| Same `?color=r,g,b` API | Same `?color=r,g,b` API |

## Requirements

- **WebXR AR-capable browser**: Chrome or WebXR Viewer on Android; on iPhone, use an XR-capable browser (e.g. WebXR Viewer, 8th Wall, or similar — native Safari does not yet support WebXR AR)
- **HTTPS or localhost** — WebXR requires a [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts)
- **Local web server** — ES modules do not work with `file://` URLs

## Setup

1. **Serve the page over HTTP/HTTPS**:

   ```bash
   # Option A: npx (Node.js)
   npx serve .

   # Option B: Python 3
   python -m http.server 8000

   # Option C: Python 2
   python -m SimpleHTTPServer 8000
   ```

2. **Open the page**:
   - Desktop: `http://localhost:8000` (or the port you used)
   - Same LAN: `http://YOUR_IP:8000` (e.g. `http://192.168.1.50:8000`)

3. **Enter AR**:
   - Tap the "START AR" button
   - Point your phone at the ground
   - The fish appears on the floor in front of you
   - Tap the fish to change the color

## Unity server

This app expects the Unity Fish Click server on the same host, port 8080:

```
http://HOSTNAME:8080/command?color=r,g,b
```

If you open the page at `http://192.168.1.50:8000`, it will send color commands to `http://192.168.1.50:8080`.

## Testing without AR

- On desktop, you can click the fish with the mouse (pointer events are supported).

## Tech stack

- [Three.js](https://threejs.org/) via CDN
- [WebXR Device API](https://immersiveweb.dev/) (immersive-ar)
- `InteractiveGroup` for tap/pointer raycasting and selection
- `local-floor` reference space for fish placement on the ground
