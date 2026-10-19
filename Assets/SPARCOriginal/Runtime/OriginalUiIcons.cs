using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sparc.Original
{
    /// <summary>Owned, antialiased white-alpha sketch icons. Tint through Image.color or GUI.color.</summary>
    public static class OriginalUiIcons
    {
        const int Size = 128;
        const int Supersample = 2;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static readonly List<Texture2D> textures = new List<Texture2D>();
        static readonly HashSet<string> supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "move", "look", "jump", "talk", "chat", "avatar", "camera", "settings", "close", "back", "next",
            "check", "info", "help", "task", "optional", "sound", "music", "play", "pause", "restart", "home",
            "oxygen", "time", "score", "shield", "level", "remaining", "exit", "confirm", "cancel", "fullscreen",
            "submit", "search", "reset", "redcell", "whitecell", "bacteria", "deposit", "obstacle", "joystick", "knob", "chevron", "send", "edit", "hair", "eyes", "mouth", "brows", "palette", "shirt", "trousers", "shoes", "glove", "glasses", "mask", "shuffle", "map", "up", "down", "point"
        };

        public static Sprite Get(string semantic)
        {
            string key = Canonical(semantic);
            if (sprites.TryGetValue(key, out var cached) && cached) return cached;
            var canvas = new InkCanvas();
            Draw(canvas, key);
            var texture = canvas.Texture("Owned UI icon / " + key);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
            sprite.name = "Sketch / " + key;
            sprites[key] = sprite;
            textures.Add(texture);
            return sprite;
        }

        public static void DisposeAll()
        {
            foreach (var sprite in sprites.Values) DestroyOwned(sprite);
            foreach (var texture in textures) DestroyOwned(texture);
            sprites.Clear();
            textures.Clear();
        }

        static void DestroyOwned(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        static string Canonical(string semantic)
        {
            string key = (semantic ?? "info").Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
            switch (key)
            {
                case "movement": case "walk": return "move";
                case "view": return "look";
                case "interact": case "dialogue": case "conversation": return "talk";
                case "message": case "messages": return "chat";
                case "outfit": case "wardrobe": case "character": case "profile": return "avatar";
                case "firstperson": case "thirdperson": case "switchcamera": return "camera";
                case "options": case "setting": case "gear": return "settings";
                case "previous": case "prev": case "left": return "back";
                case "right": case "continue": case "forward": return "next";
                case "done": case "complete": case "completed": return "check";
                case "information": return "info";
                case "question": case "tutorial": return "help";
                case "objective": case "objectives": case "tasks": case "journal": case "workbook": return "task";
                case "audio": case "volume": case "sfx": return "sound";
                case "start": case "resume": return "play";
                case "replay": case "retry": return "restart";
                case "menu": case "mainmenu": return "home";
                case "o2": return "oxygen";
                case "red": case "rbc": return "redcell";
                case "white": case "wbc": return "whitecell";
                case "clock": case "timer": return "time";
                case "points": case "reward": case "star": return "score";
                case "defense": return "shield";
                case "germs": case "rem": return "remaining";
                case "quit": case "leave": case "logout": return "exit";
                case "yes": case "accept": case "ok": return "confirm";
                case "no": case "reject": return "cancel";
                case "expand": return "fullscreen";
                case "upload": return "submit";
                case "pencil": case "write": return "edit";
                case "skin": case "skincolor": case "color": case "colour": return "palette";
                case "top": case "tops": return "shirt";
                case "bottom": case "bottoms": case "pants": return "trousers";
                case "shoe": case "footwear": return "shoes";
                case "gloves": return "glove";
                case "eyewear": return "glasses";
                case "eyebrows": case "eyebrow": return "brows";
                case "eye": return "eyes";
                case "random": case "randomize": return "shuffle";
                case "knowledgemap": case "knowledge": return "map";
                case "arrowup": case "uparrow": case "up2": case "increase": return "up";
                case "arrowdown": case "downarrow": case "down2": case "decrease": return "down";
                case "handpointer": case "pointer": case "pointinghand": case "touch": return "point";
                case "find": return "search";
                case "restore": case "default": return "reset";
            }
            return supported.Contains(key) ? key : "info";
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Vector2[] Points(params float[] coordinates)
        {
            var result = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = V(coordinates[i * 2], coordinates[i * 2 + 1]);
            return result;
        }

        static void Draw(InkCanvas c, string key)
        {
            switch (key)
            {
                case "move":
                    c.Arrow(64, 61, 64, 20, 10); c.Arrow(67, 64, 108, 64, 10);
                    c.Arrow(64, 67, 64, 108, 10); c.Arrow(61, 64, 20, 64, 10); c.Dot(64, 64, 4);
                    break;
                case "look":
                    c.Path(Points(16, 65, 30, 47, 49, 38, 65, 35, 83, 39, 101, 50, 113, 65,
                        100, 80, 83, 90, 64, 94, 45, 89, 28, 80), true);
                    c.Circle(64, 65, 18); c.Dot(64, 65, 6); c.L(27, 27, 21, 20); c.L(64, 23, 64, 15); c.L(102, 28, 109, 20);
                    break;
                case "jump":
                    c.Circle(43, 37, 9); c.Path(Points(43, 47, 40, 69, 28, 91, 17, 94));
                    c.Path(Points(41, 68, 60, 83, 66, 102)); c.Path(Points(41, 53, 23, 61, 17, 51));
                    c.Path(Points(44, 52, 62, 40, 67, 27)); c.Arrow(91, 97, 91, 23, 14); c.L(18, 111, 107, 111);
                    break;
                case "talk":
                    c.Path(Points(22, 25, 107, 25, 107, 83, 59, 83, 37, 103, 40, 83, 22, 83), true);
                    c.Dot(43, 54, 4); c.Dot(65, 54, 4); c.Dot(87, 54, 4);
                    break;
                case "chat":
                    c.Path(Points(46, 54, 110, 54, 110, 96, 98, 96, 99, 111, 79, 96, 46, 96), true);
                    c.Path(Points(17, 19, 88, 19, 88, 70, 51, 70, 30, 87, 31, 70, 17, 70), true, 0.10f);
                    c.L(31, 37, 72, 37); c.L(31, 51, 60, 51);
                    break;
                case "avatar":
                    c.Circle(64, 40, 19); c.Arc(64, 98, 37, 180, 360); c.L(27, 98, 27, 108); c.L(27, 108, 101, 108); c.L(101, 108, 101, 98);
                    c.Dot(57, 38, 2); c.Dot(71, 38, 2); c.Arc(64, 43, 7, 20, 160);
                    break;
                case "camera":
                    c.Path(Points(19, 39, 42, 39, 49, 27, 80, 27, 87, 39, 109, 39, 109, 100, 19, 100), true);
                    c.Circle(65, 68, 22); c.Circle(65, 68, 14, 0.35f); c.Dot(96, 50, 3);
                    break;
                case "settings":
                    var gear = new Vector2[32];
                    for (int i = 0; i < gear.Length; i++) { float angle = (i * 360f / gear.Length - 90) * Mathf.Deg2Rad; float radius = i % 4 < 2 ? 43 : 34; gear[i] = V(64 + Mathf.Cos(angle) * radius, 64 + Mathf.Sin(angle) * radius); }
                    c.Path(gear, true); c.Circle(64, 64, 14);
                    break;
                case "close":
                    c.L(31, 31, 97, 97, 4); c.L(97, 31, 31, 97, 4);
                    break;
                case "back":
                    c.Arrow(106, 64, 24, 64, 25);
                    break;
                case "next":
                    c.Arrow(22, 64, 104, 64, 25);
                    break;
                case "check":
                    c.Path(Points(25, 66, 50, 91, 105, 34), false, 0, 5);
                    break;
                case "info":
                    c.Circle(64, 64, 43); c.Dot(64, 41, 4); c.L(64, 57, 64, 91, 4); c.L(55, 92, 74, 92);
                    break;
                case "help":
                    c.Circle(64, 64, 45); c.Path(Points(46, 44, 48, 35, 58, 30, 71, 31, 81, 39, 82, 49, 75, 58, 65, 64, 63, 75)); c.Dot(63, 91, 4);
                    break;
                case "task":
                    c.Path(Points(29, 27, 101, 27, 101, 109, 23, 109, 23, 27, 42, 27));
                    c.Path(Points(46, 18, 79, 18, 79, 36, 46, 36), true);
                    c.Path(Points(36, 52, 42, 58, 51, 47)); c.L(61, 53, 88, 53);
                    c.Path(Points(36, 75, 42, 81, 51, 70)); c.L(61, 76, 88, 76); c.L(37, 96, 49, 96); c.L(61, 96, 88, 96);
                    break;
                case "optional":
                    for (int i = 0; i < 8; i++) c.Arc(64, 64, 43, i * 45, i * 45 + 27);
                    c.Path(Points(64, 37, 72, 55, 92, 58, 77, 72, 80, 92, 64, 82, 47, 92, 51, 72, 36, 58, 56, 55), true, 0.08f);
                    break;
                case "sound":
                    c.Path(Points(20, 51, 38, 51, 59, 32, 59, 97, 38, 78, 20, 78), true, 0.08f);
                    c.Arc(58, 64, 27, -52, 52); c.Arc(58, 64, 43, -52, 52); c.Arc(58, 64, 58, -48, 48);
                    break;
                case "music":
                    c.Path(Points(49, 94, 49, 35, 99, 23, 99, 82)); c.L(49, 47, 99, 35);
                    c.Ellipse(36, 95, 13, 10, 0.10f); c.Ellipse(86, 83, 13, 10, 0.10f);
                    break;
                case "play":
                    c.Path(Points(41, 24, 103, 64, 41, 105), true, 0.11f, 4);
                    break;
                case "pause":
                    c.Path(Points(32, 24, 50, 24, 50, 104, 32, 104), true, 0.10f);
                    c.Path(Points(78, 24, 96, 24, 96, 104, 78, 104), true, 0.10f);
                    break;
                case "restart":
                    c.Arc(64, 66, 40, -140, 140); c.Path(Points(20, 26, 20, 47, 41, 45));
                    c.Path(Points(56, 48, 81, 65, 56, 81), true, 0.08f);
                    break;
                case "home":
                    c.Path(Points(16, 58, 64, 19, 112, 58)); c.Path(Points(29, 52, 29, 108, 99, 108, 99, 52));
                    c.Path(Points(53, 108, 53, 77, 76, 77, 76, 108)); c.Dot(69, 94, 2);
                    break;
                case "oxygen":
                    c.Circle(43, 55, 24); c.Circle(87, 78, 24); c.L(61, 42, 78, 51); c.L(51, 77, 66, 87);
                    c.Dot(34, 47, 3); c.Dot(95, 85, 3); c.L(95, 24, 95, 36); c.L(89, 30, 101, 30);
                    break;
                case "time":
                    c.Circle(64, 68, 39); c.L(64, 68, 64, 43, 4); c.L(64, 68, 85, 80, 4); c.Dot(64, 68, 4);
                    c.L(54, 14, 75, 14); c.L(64, 15, 64, 28); c.L(93, 28, 102, 37);
                    c.L(64, 34, 64, 38); c.L(29, 68, 34, 68); c.L(64, 99, 64, 103); c.L(94, 68, 99, 68);
                    break;
                case "score":
                    c.Path(Star(64, 62, 39, 18), true, 0.10f); c.L(21, 105, 35, 98); c.L(93, 98, 107, 105); c.L(64, 111, 64, 119);
                    break;
                case "shield":
                    c.Path(Points(64, 18, 103, 33, 101, 70, 88, 94, 64, 111, 41, 94, 27, 70, 25, 33), true, 0.07f);
                    c.Path(Points(45, 61, 59, 76, 84, 48), false, 0, 4);
                    break;
                case "level":
                    c.Path(Points(21, 88, 46, 88, 46, 105, 21, 105), true); c.Path(Points(51, 67, 76, 67, 76, 105, 51, 105), true);
                    c.Path(Points(81, 45, 106, 45, 106, 105, 81, 105), true); c.Arrow(27, 65, 76, 20, 12);
                    break;
                case "remaining":
                    Germ(c, 40, 45, 16); Germ(c, 89, 49, 13); Germ(c, 65, 91, 17);
                    break;
                case "exit":
                    c.Path(Points(66, 24, 29, 24, 29, 105, 66, 105)); c.Path(Points(31, 25, 61, 34, 61, 112, 31, 104));
                    c.Dot(51, 71, 3); c.Arrow(69, 66, 113, 66, 14);
                    break;
                case "confirm":
                    c.Circle(64, 64, 43); c.Path(Points(39, 65, 57, 83, 90, 46), false, 0, 4);
                    break;
                case "cancel":
                    c.Circle(64, 64, 43); c.L(34, 95, 95, 34, 4);
                    break;
                case "fullscreen":
                    c.Path(Points(21, 47, 21, 21, 47, 21)); c.Path(Points(81, 21, 107, 21, 107, 47));
                    c.Path(Points(107, 81, 107, 107, 81, 107)); c.Path(Points(47, 107, 21, 107, 21, 81));
                    c.L(22, 22, 44, 44); c.L(106, 22, 84, 44); c.L(106, 106, 84, 84); c.L(22, 106, 44, 84);
                    break;
                case "submit":
                    c.Path(Points(25, 23, 80, 23, 100, 43, 100, 108, 25, 108), true); c.Path(Points(79, 24, 79, 44, 99, 44));
                    c.Arrow(63, 92, 63, 51, 13); c.L(39, 93, 86, 93);
                    break;
                case "redcell":
                    c.Ellipse(64, 66, 47, 36, 0.05f); c.Ellipse(64, 64, 26, 17, 0.07f);
                    c.Arc(64, 68, 35, 19, 152, 0.35f); c.Dot(54, 57, 3); c.Dot(73, 57, 3); c.Arc(64, 64, 9, 20, 160);
                    break;
                case "whitecell":
                    c.Circle(64, 64, 44); c.Path(Points(64, 35, 88, 44, 86, 67, 78, 82, 64, 93, 50, 82, 42, 67, 40, 44), true, 0.06f);
                    c.Dot(55, 57, 3); c.Dot(74, 57, 3); c.Arc(64, 64, 10, 20, 160);
                    c.L(21, 28, 17, 23); c.L(104, 27, 109, 22); c.L(20, 99, 15, 105); c.L(105, 98, 111, 104);
                    break;
                case "bacteria":
                    Germ(c, 64, 64, 31); c.Arc(64, 68, 10, 20, 160);
                    c.Dot(49, 71, 2); c.Dot(77, 49, 2);
                    break;
                case "deposit":
                    c.Path(Points(20, 74, 20, 106, 108, 106, 108, 74)); c.L(21, 76, 42, 76); c.L(85, 76, 107, 76);
                    c.Arrow(64, 47, 64, 91, 14); c.Circle(48, 29, 10); c.Circle(77, 26, 11); c.L(58, 28, 66, 27);
                    break;
                case "obstacle":
                    c.Path(Points(23, 79, 31, 48, 50, 25, 80, 27, 102, 49, 111, 79, 95, 103, 39, 106), true, 0.05f);
                    c.Path(Points(32, 79, 41, 53, 55, 39)); c.L(51, 91, 63, 78); c.L(65, 96, 79, 81); c.L(79, 98, 92, 85);
                    break;
                case "joystick":
                    c.Circle(64, 64, 44); c.Circle(64, 64, 19); c.L(64, 25, 64, 34); c.L(94, 64, 104, 64);
                    c.L(64, 94, 64, 103); c.L(25, 64, 34, 64); c.Dot(64, 64, 4);
                    break;
                case "knob":
                    c.Circle(64, 64, 39); c.Circle(64, 64, 29, 0.35f); c.L(64, 35, 64, 49, 4); c.Dot(64, 65, 5);
                    break;
                case "chevron":
                    c.Path(Points(43, 24, 84, 64, 43, 104), false, 0, 4);
                    break;
                case "send":
                    c.Path(Points(16, 55, 111, 18, 82, 111, 61, 76, 16, 55), true, 0.07f);
                    c.Path(Points(61, 76, 95, 35, 48, 64)); c.L(62, 78, 52, 104); c.L(52, 104, 75, 94);
                    break;
                case "edit":
                    c.Path(Points(31, 81, 83, 25, 105, 45, 52, 102, 20, 110, 31, 81), true, 0.07f);
                    c.L(73, 36, 94, 56); c.L(32, 82, 52, 101); c.L(23, 101, 28, 106); c.L(44, 86, 84, 44);
                    break;
                case "hair":
                    c.Path(Points(24, 81, 19, 57, 26, 35, 45, 20, 70, 17, 96, 31, 108, 56, 104, 82));
                    c.Path(Points(25, 80, 31, 53, 47, 45, 51, 31, 63, 45, 77, 40, 82, 29, 91, 47, 100, 79));
                    c.Path(Points(34, 81, 38, 97, 51, 108, 75, 108, 91, 95, 96, 81));
                    c.Path(Points(30, 60, 25, 76, 35, 72)); c.Path(Points(96, 58, 103, 75, 94, 71));
                    break;
                case "eyes":
                    c.Ellipse(39, 64, 22, 30); c.Ellipse(91, 64, 22, 30); c.Ellipse(43, 64, 8, 15, 0.12f); c.Ellipse(87, 64, 8, 15, 0.12f);
                    c.Dot(43, 64, 3); c.Dot(87, 64, 3); c.L(23, 37, 18, 28); c.L(104, 37, 110, 28);
                    break;
                case "mouth":
                    c.Path(Points(19, 53, 35, 58, 64, 60, 93, 58, 110, 53, 101, 80, 83, 98, 64, 105, 44, 98, 28, 81), true, 0.04f);
                    c.Path(Points(23, 63, 43, 73, 64, 77, 86, 73, 106, 63));
                    c.Path(Points(45, 97, 55, 87, 68, 86, 82, 98)); c.Arc(28, 46, 11, 205, 270); c.Arc(101, 46, 11, -90, -25);
                    break;
                case "brows":
                    c.Path(Points(17, 51, 35, 37, 52, 42, 55, 50, 34, 45, 19, 58), true, 0.10f);
                    c.Path(Points(73, 50, 76, 42, 94, 37, 111, 51, 109, 58, 94, 45), true, 0.10f);
                    c.Arc(36, 83, 14, 197, 343); c.Arc(93, 83, 14, 197, 343);
                    break;
                case "palette":
                    c.Path(Points(30, 99, 17, 81, 15, 57, 24, 35, 44, 21, 72, 18, 96, 29, 112, 49, 110, 66, 96, 70, 80, 65, 69, 74, 75, 94, 63, 109, 45, 108), true, 0.04f);
                    c.Circle(37, 49, 7); c.Circle(59, 35, 7); c.Circle(85, 40, 7); c.Circle(35, 78, 7); c.Ellipse(57, 92, 7, 9);
                    break;
                case "shirt":
                    c.Path(Points(47, 23, 33, 29, 13, 52, 30, 70, 40, 58, 40, 108, 89, 108, 89, 58, 99, 70, 115, 52, 96, 29, 81, 23), true, 0.05f);
                    c.Arc(64, 21, 17, 0, 180); c.L(48, 98, 80, 98); c.L(92, 41, 105, 55); c.L(23, 55, 36, 41);
                    break;
                case "trousers":
                    c.Path(Points(32, 20, 97, 20, 105, 108, 75, 108, 64, 61, 54, 108, 24, 108), true, 0.05f);
                    c.L(32, 34, 98, 34); c.L(63, 35, 63, 53); c.Dot(65, 27, 2); c.L(28, 98, 56, 98); c.L(74, 98, 103, 98);
                    c.Path(Points(38, 36, 38, 46, 30, 51)); c.Path(Points(90, 36, 90, 46, 99, 51));
                    break;
                case "shoes":
                    c.Path(Points(23, 34, 49, 35, 58, 55, 83, 68, 106, 77, 112, 91, 106, 103, 22, 103, 18, 90, 20, 66), true, 0.05f);
                    c.L(20, 90, 111, 90); c.Path(Points(25, 48, 39, 53, 49, 44)); c.L(56, 57, 46, 67); c.L(66, 62, 57, 73); c.L(76, 68, 68, 78);
                    break;
                case "glove":
                    c.Path(Points(43, 104, 31, 85, 19, 65, 20, 56, 28, 54, 43, 69, 37, 29, 41, 21, 48, 22, 53, 57,
                        52, 19, 58, 15, 65, 19, 68, 55, 70, 23, 77, 20, 84, 26, 82, 59, 88, 36, 95, 34, 101, 41, 95, 85, 82, 104), true, 0.04f);
                    c.Path(Points(43, 103, 83, 103, 83, 114, 43, 114), true); c.Path(Points(49, 75, 57, 70, 72, 73));
                    break;
                case "glasses":
                    c.Circle(35, 68, 23); c.Circle(94, 68, 23); c.Arc(64, 64, 7, 200, 340);
                    c.L(13, 64, 18, 41); c.L(18, 41, 29, 35); c.L(116, 64, 111, 41); c.L(111, 41, 100, 35);
                    c.L(25, 54, 20, 60); c.L(84, 54, 79, 60);
                    break;
                case "mask":
                    c.Path(Points(31, 40, 64, 48, 96, 40, 96, 85, 64, 102, 31, 85), true, 0.04f);
                    c.Path(Points(30, 49, 18, 42, 13, 51, 16, 75, 30, 82)); c.Path(Points(97, 49, 109, 42, 115, 51, 111, 75, 97, 82));
                    c.Path(Points(40, 60, 64, 65, 87, 60)); c.Path(Points(40, 74, 64, 79, 87, 74)); c.L(40, 87, 87, 87);
                    break;
                case "shuffle":
                    c.Path(Points(18, 32, 37, 32, 88, 96, 111, 96)); c.Path(Points(18, 96, 37, 96, 57, 71));
                    c.Path(Points(72, 52, 88, 32, 111, 32)); c.Path(Points(98, 19, 111, 32, 98, 45)); c.Path(Points(98, 83, 111, 96, 98, 109));
                    break;
                case "map":
                    c.Path(Points(17, 32, 48, 19, 80, 33, 111, 20, 111, 98, 80, 111, 48, 97, 17, 110), true, 0.04f);
                    c.L(48, 20, 48, 97); c.L(80, 34, 80, 110);
                    c.Path(Points(31, 88, 38, 75, 55, 76)); c.Path(Points(61, 75, 70, 64, 70, 52)); c.Circle(92, 47, 8); c.L(88, 54, 93, 62); c.L(97, 54, 93, 62);
                    break;
                case "up":
                    c.Arrow(64, 109, 64, 21, 26);
                    break;
                case "down":
                    c.Arrow(64, 19, 64, 107, 26);
                    break;
                case "point":
                    c.Path(Points(45, 108, 34, 89, 21, 69, 22, 60, 30, 56, 46, 72, 45, 24, 50, 17, 58, 19, 61, 57,
                        66, 51, 73, 52, 78, 60, 84, 56, 92, 60, 94, 67, 102, 67, 108, 75, 104, 95, 93, 108), true, 0.04f);
                    c.Path(Points(45, 107, 94, 107, 94, 118, 45, 118), true); c.L(62, 72, 64, 86); c.L(78, 74, 78, 87); c.L(93, 78, 91, 90);
                    c.L(20, 30, 30, 34); c.L(31, 9, 38, 17); c.L(70, 8, 64, 16); c.L(84, 25, 74, 29);
                    break;
                case "search":
                    c.Circle(55, 54, 30); c.L(77, 77, 109, 109, 6); c.Arc(55, 54, 21, 193, 252, 0.35f);
                    break;
                case "reset":
                    c.Arc(64, 65, 41, -55, 250); c.Path(Points(36, 20, 53, 28, 44, 45));
                    c.L(52, 53, 76, 77); c.L(76, 53, 52, 77);
                    break;
            }
        }

        static Vector2[] Star(float x, float y, float outer, float inner)
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (-90 + i * 36) * Mathf.Deg2Rad, radius = i % 2 == 0 ? outer : inner;
                points[i] = V(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
            }
            return points;
        }

        static void Germ(InkCanvas c, float x, float y, float radius)
        {
            c.Circle(x, y, radius);
            for (int i = 0; i < 6; i++) { float a = (i * 60 + 15) * Mathf.Deg2Rad; c.L(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(a) * (radius + 7), y + Mathf.Sin(a) * (radius + 7)); }
            c.Dot(x - radius * 0.25f, y - 2, 2); c.Dot(x + radius * 0.28f, y + 3, 2);
        }

        sealed class InkCanvas
        {
            const int Pixels = Size * Supersample;
            readonly float[] alpha = new float[Pixels * Pixels];

            public void L(float x1, float y1, float x2, float y2, float width = 3)
            {
                Segment(V(x1 + 1.05f, y1 - 0.8f), V(x2 - 0.6f, y2 + 0.9f), width * 0.57f, 0.25f);
                Segment(V(x1, y1), V(x2, y2), width, 1);
            }

            public void Path(Vector2[] points, bool closed = false, float fill = 0, float width = 3)
            {
                if (points.Length < 2) return;
                if (fill > 0) Fill(points, fill);
                for (int i = 1; i < points.Length; i++) L(points[i - 1].x, points[i - 1].y, points[i].x, points[i].y, width);
                if (closed) L(points[points.Length - 1].x, points[points.Length - 1].y, points[0].x, points[0].y, width);
            }

            public void Arrow(float x1, float y1, float x2, float y2, float head)
            {
                L(x1, y1, x2, y2);
                var direction = (V(x2, y2) - V(x1, y1)).normalized;
                var side = V(-direction.y, direction.x);
                var a = V(x2, y2) - direction * head + side * head * 0.72f;
                var b = V(x2, y2) - direction * head - side * head * 0.72f;
                L(a.x, a.y, x2, y2); L(b.x, b.y, x2, y2);
            }

            public void Circle(float x, float y, float radius, float opacity = 1) => Ellipse(x, y, radius, radius, 0, opacity);

            public void Ellipse(float x, float y, float rx, float ry, float fill = 0, float opacity = 1)
            {
                var points = new Vector2[65];
                for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2 / 64; points[i] = V(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry); }
                if (fill > 0) Fill(points, fill);
                for (int i = 1; i < points.Length; i++)
                {
                    Segment(points[i - 1] + V(0.8f, -0.6f), points[i] + V(0.8f, -0.6f), 1.5f, opacity * 0.26f);
                    Segment(points[i - 1], points[i], 3, opacity);
                }
            }

            public void Arc(float x, float y, float radius, float begin, float end, float opacity = 1)
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(end - begin) / 5));
                var previous = V(x + Mathf.Cos(begin * Mathf.Deg2Rad) * radius, y + Mathf.Sin(begin * Mathf.Deg2Rad) * radius);
                for (int i = 1; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(begin, end, i / (float)steps) * Mathf.Deg2Rad;
                    var current = V(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    Segment(previous + V(0.7f, -0.7f), current + V(0.7f, -0.7f), 1.5f, opacity * 0.25f);
                    Segment(previous, current, 3, opacity); previous = current;
                }
            }

            public void Dot(float x, float y, float radius)
            {
                int minX = Mathf.Max(0, Mathf.FloorToInt((x - radius - 1) * Supersample));
                int maxX = Mathf.Min(Pixels - 1, Mathf.CeilToInt((x + radius + 1) * Supersample));
                int minY = Mathf.Max(0, Mathf.FloorToInt((y - radius - 1) * Supersample));
                int maxY = Mathf.Min(Pixels - 1, Mathf.CeilToInt((y + radius + 1) * Supersample));
                for (int py = minY; py <= maxY; py++) for (int px = minX; px <= maxX; px++)
                {
                    float distance = Vector2.Distance(V((px + 0.5f) / Supersample, (py + 0.5f) / Supersample), V(x, y));
                    Set(px, py, Mathf.Clamp01((radius - distance) * Supersample + 0.5f));
                }
            }

            void Segment(Vector2 a, Vector2 b, float width, float opacity)
            {
                float margin = width * 0.5f + 1;
                int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - margin) * Supersample));
                int maxX = Mathf.Min(Pixels - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + margin) * Supersample));
                int minY = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - margin) * Supersample));
                int maxY = Mathf.Min(Pixels - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + margin) * Supersample));
                var delta = b - a; float length = delta.sqrMagnitude;
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    var point = V((x + 0.5f) / Supersample, (y + 0.5f) / Supersample);
                    float t = length > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / length) : 0;
                    float distance = Vector2.Distance(point, a + delta * t);
                    Set(x, y, Mathf.Clamp01((width * 0.5f - distance) * Supersample + 0.5f) * opacity);
                }
            }

            void Fill(Vector2[] polygon, float opacity)
            {
                for (int y = 0; y < Pixels; y++) for (int x = 0; x < Pixels; x++)
                {
                    float px = (x + 0.5f) / Supersample, py = (y + 0.5f) / Supersample; bool inside = false;
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                    {
                        var a = polygon[i]; var b = polygon[j];
                        if ((a.y > py) != (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    if (inside) Set(x, y, opacity);
                }
            }

            void Set(int x, int y, float value)
            {
                int index = y * Pixels + x;
                if (value > alpha[index]) alpha[index] = value;
            }

            public Texture2D Texture(string name)
            {
                var colors = new Color32[Size * Size];
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                {
                    float sum = 0;
                    for (int sy = 0; sy < Supersample; sy++) for (int sx = 0; sx < Supersample; sx++)
                        sum += alpha[(y * Supersample + sy) * Pixels + x * Supersample + sx];
                    // UI coordinates above use a top-left origin; Texture2D uses bottom-left.
                    colors[(Size - 1 - y) * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(sum / (Supersample * Supersample) * 255));
                }
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                texture.SetPixels32(colors); texture.Apply(false, true);
                return texture;
            }
        }
    }
}
