using System.IO;
using Pointless.Core;
using Unity.Profiling;
using UnityEngine;

namespace Pointless.Editor
{
    public static class Robot
    {
        const int Wide = 1080;
        const int High = 2340;

        static RenderTexture frame;
        static Rng rng = new Rng(7);

        public static string Lift(float nudgeX = 0f, float nudgeY = 0f)
        {
            GameView view = View();
            Game game = view.State;
            if (game.Over) return "over";
            if (!Choose(game, out int slot, out int place)) return "stuck";
            view.Press(view.SlotPoint(slot));
            view.Drag(view.PlacePoint(slot, place) + new Vector2(nudgeX, nudgeY));
            return $"slot {slot} place {place}";
        }

        public static string Drop()
        {
            GameView view = View();
            view.Release();
            return Report(view.State);
        }

        public static string Move()
        {
            string lifted = Lift();
            return lifted == "over" || lifted == "stuck" ? lifted : Drop();
        }

        public static string Tap(float x, float y)
        {
            GameView view = View();
            view.Press(new Vector2(x, y));
            view.Release();
            return Report(view.State);
        }

        public static string Frame()
        {
            if (frame == null) frame = new RenderTexture(Wide, High, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Camera.main.targetTexture = frame;
            return $"{Wide}x{High}";
        }

        public static string Shoot(string path)
        {
            Camera camera = Camera.main;
            RenderTexture kept = camera.targetTexture;
            RenderTexture target = kept != null ? kept : RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = before;
            camera.targetTexture = kept;
            if (kept == null) RenderTexture.ReleaseTemporary(target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return path;
        }

        public static string Later(string path, float delay)
        {
            double due = UnityEditor.EditorApplication.timeSinceStartup + delay;
            void Wait()
            {
                if (UnityEditor.EditorApplication.timeSinceStartup < due) return;
                UnityEditor.EditorApplication.update -= Wait;
                Shoot(path);
            }
            UnityEditor.EditorApplication.update += Wait;
            return path;
        }

        public static string Report(Game game) =>
            $"score {game.Score} combo {game.Combo} misses {game.Misses} lines {game.Lines} filled {Bits.Count(game.Board)} over {game.Over} undo {game.CanUndo}";

        static ProfilerRecorder garbage, batches, calls, passes, triangles;

        public static string Meter()
        {
            garbage = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 240);
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 240);
            calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 240);
            passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 240);
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 240);
            return "metering";
        }

        public static string Reading()
        {
            string result = $"frames {garbage.Count} | garbage bytes per frame avg {Average(garbage):0} max {Peak(garbage)} | batches {Average(batches):0.0} | draw calls {Average(calls):0.0} | set pass {Average(passes):0.0} | triangles {Average(triangles):0}";
            garbage.Dispose();
            batches.Dispose();
            calls.Dispose();
            passes.Dispose();
            triangles.Dispose();
            return result;
        }

        static double Average(ProfilerRecorder recorder)
        {
            if (recorder.Count == 0) return 0;
            double total = 0;
            for (int i = 0; i < recorder.Count; i++) total += recorder.GetSample(i).Value;
            return total / recorder.Count;
        }

        static long Peak(ProfilerRecorder recorder)
        {
            long peak = 0;
            for (int i = 0; i < recorder.Count; i++) peak = System.Math.Max(peak, recorder.GetSample(i).Value);
            return peak;
        }

        static GameView View() => Object.FindAnyObjectByType<GameView>();

        static bool Choose(Game game, out int slot, out int place)
        {
            slot = place = -1;
            int best = int.MinValue;
            for (int i = 0; i < Game.Slots; i++)
            {
                if (game.Tray[i] < 0) continue;
                int found = Generator.Best(game.Board, Shapes.All[game.Tray[i]], ref rng, out int value);
                if (found < 0 || value <= best) continue;
                best = value;
                slot = i;
                place = found;
            }
            return slot >= 0;
        }
    }
}
