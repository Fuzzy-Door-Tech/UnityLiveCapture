# Video Streaming Server

## URP raycaster integration

Before Unity 6000.5, `InjectionPointRenderFeature` invokes `onExecute` after the camera renders. The virtual-camera graphics raycaster is the only subscriber in this package. On Unity 6000.5 and newer, URP's RenderGraph path no longer calls that legacy pass. The raycaster instead listens for `RenderPipelineManager.endCameraRendering`, filters for its dedicated camera, draws into its own picking textures, and submits those commands before GPU readback. Its camera output texture has a depth attachment, as RenderGraph requires.

The `onExecute` event remains available on the older URP path. The newer path does not add an event with a different rendering-data contract. Run `UrpGraphicsRaycasterTests.Raycast_VisibleCube_ReturnsObject` in the Unity Test Runner to check the newer camera callback and object picking.

## A Video Streaming Server Based on RTSP and H.264

The network layer is based on [SharpRTSP](https://github.com/ngraziano/SharpRTSP)

H.264 Encoding relies on a custom native plugin supporting Windows + Nvidia Hardware at the moment (so that we can benefit from hardware accelerated encoding), see `Native~` directory for plugin source

## Usage

The tool is meant to be used through 2 classes:

* `VideoStreamingServer` implements the video streaming server, this class exposes methods for starting and stopping the server

* `VideoStreamSource` is responsible for sending a `Camera` output as a video stream

To set up video streaming, the following steps are needed:

* Instanciate a `VideoStreamingServer`, start it on a given port, eventually using credentials: `m_VideoStreamingServer.Start(port, null, null);`

* Assign a `VideoStreamSource` to the camera whose output is to be streamed

* For the video stream to be updated, one must explicitely call `ProcessStream` on update, passing the relevant camera (the one the `VideoStreamSource` was assigned to): `m_VideoStreamingServer.ProcessStream(m_Camera);`

`VideoStreamSource` exposes resolution, framerate and bitrate, users are free to tweak those to balance stream quality and responsiveness based on their use case.

