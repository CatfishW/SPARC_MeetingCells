# Independent SPARC Meeting Cells repository scope

This repository contains the standalone SPARC_MeetingCells Unity/WebGL module:
- the playable Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity scene and its serialized dependency closure;
- first-party runtime/editor/tests, generated pixel/sketch art, WebGL templates and required media;
- current verified development/release builds and current deliverables;
- current source-bound audit, visual-gallery, browser-acceptance and generated-art provenance.

Excluded: Unity Library, Logs, UserSettings, local Unity/toolchain installers, unrelated SPARC modules/scenes, and superseded historical acceptance runs. The original project is not copied into this repository.

The project was further trimmed to the dependency closure of `SPARC_MeetingCells.unity` (GUID-reachable assets plus `Assets/SPARCOriginal` code, Resources, StreamingAssets and WebGL templates). The intermediate `MeetingCells_NewEnv.unity` import scene, `Assets/SPARC/Generated` leftovers and unreferenced Photon demo materials were removed; `Tools/restore_original_assets.py` recreates the import scene if the builder pipeline is needed again.
