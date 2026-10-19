"""Structural guard for the development-only observer's scene-reload installation.

The observer is intentionally excluded from Editor/native tests and release players.
This checks the installation contract in its actual source; the browser journey's
real Play again action supplies the WebGL lifecycle proof.
"""
from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "Assets/SPARCOriginal/Runtime/OriginalReadOnlyProbe.cs").read_text()


def method_body(name):
    match = re.search(rf"\b(?:static\s+)?void\s+{name}\s*\([^)]*\)\s*(=>|\{{)", SOURCE)
    if match is None:
        raise AssertionError(f"missing observer method: {name}")
    if match.group(1) == "=>":
        return SOURCE[match.end():SOURCE.index(";", match.end())]
    depth = 1
    for index in range(match.end(), len(SOURCE)):
        depth += (SOURCE[index] == "{") - (SOURCE[index] == "}")
        if not depth:
            return SOURCE[match.end():index]
    raise AssertionError(f"unclosed observer method: {name}")


def compact(value):
    return re.sub(r"\s+", "", value)


class OriginalProbeLifecycleContract(unittest.TestCase):
    def test_entire_observer_remains_development_webgl_only(self):
        lines = [line.strip() for line in SOURCE.splitlines() if line.strip()]
        self.assertEqual(lines[0], "#if UNITY_WEBGL && DEVELOPMENT_BUILD")
        self.assertEqual(lines[-1], "#endif")
        self.assertEqual(sum(line.startswith("#if") for line in lines), 1)

    def test_startup_and_each_scene_load_attach_an_observer(self):
        self.assertRegex(SOURCE, r"\[RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType.BeforeSceneLoad\)\]\s*static void ObserveSceneLoads")
        self.assertRegex(SOURCE, r"\[RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType.AfterSceneLoad\)\]\s*static void Install")
        self.assertEqual(compact(method_body("Install")), "Attach(SceneManager.GetActiveScene())")
        self.assertRegex(SOURCE, r"static void SceneLoaded\(Scene scene,LoadSceneMode mode\)")
        self.assertEqual(compact(method_body("SceneLoaded")), "Attach(scene)")

    def test_subscription_survives_old_observer_destruction_without_duplication(self):
        body = compact(method_body("ObserveSceneLoads"))
        self.assertEqual(body, "SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;")
        # The app-level subscription must not be removed when replay unloads its old observer.
        self.assertEqual(SOURCE.count("sceneLoaded-=SceneLoaded"), 1)
        self.assertNotIn("DontDestroyOnLoad", SOURCE)

    def test_loaded_scene_is_scoped_and_repeated_attachment_is_idempotent(self):
        body = compact(method_body("Attach"))
        self.assertIn("FindObjectsByType<OriginalRuntime>(FindObjectsInactive.Include,FindObjectsSortMode.None)", body)
        self.assertIn("if(r.gameObject.scene==scene&&!r.GetComponent<OriginalReadOnlyProbe>())r.gameObject.AddComponent<OriginalReadOnlyProbe>();", body)
        self.assertNotIn("FindFirstObjectByType", body)
        self.assertIn("runtime=GetComponent<OriginalRuntime>();", compact(method_body("Start")))
        self.assertNotRegex(SOURCE, r"static\s+OriginalRuntime\b")


if __name__ == "__main__":
    unittest.main()
