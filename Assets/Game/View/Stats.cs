using System;
using System.IO;
using UnityEngine;

namespace Pointless
{
    [Serializable]
    public sealed class Stat
    {
        public string time;
        public long ms;
        public string session;
        public string install;
        public string player;
        public int pid;
        public string ev;
        public string mode;
        public int game;
        public int move;
        public int slot = -1;
        public int shape = -1;
        public int size;
        public int place = -1;
        public int lines;
        public int points;
        public int combo;
        public int score;
        public int best;
        public int perfects;
        public int triples;
        public int fill;
        public int fits;
        public int misses;
        public int hovers;
        public long board;
        public string tray;
        public bool perfect;
        public bool triple;
        public bool broke;
        public bool dealt;
        public bool over;
        public bool record;
        public int think;
        public int drag;
        public int duration;
        public int value;
        public string info;
    }

    public sealed class Stats : IDisposable
    {
        public static bool Muted;

        readonly StreamWriter writer;
        readonly string session = Guid.NewGuid().ToString("N").Substring(0, 8);

        const string InstallKey = "install";

        readonly string install;

        public string Player = string.Empty;
        public int PlayerIndex;

        public static string Folder => Path.Combine(Application.persistentDataPath, "stats");

        public Stats()
        {
            install = PlayerPrefs.GetString(InstallKey, string.Empty);
            if (install.Length == 0)
            {
                install = Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(InstallKey, install);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory(Folder);
            var file = new FileStream(Path.Combine(Folder, $"{DateTime.Now:yyyy-MM-dd}.jsonl"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            writer = new StreamWriter(file) { AutoFlush = true };
        }

        public void Write(Stat stat)
        {
            if (Muted) return;
            DateTimeOffset now = DateTimeOffset.Now;
            stat.time = now.ToString("o");
            stat.ms = now.ToUnixTimeMilliseconds();
            stat.session = session;
            stat.install = install;
            stat.player = Player;
            stat.pid = PlayerIndex;
            writer.WriteLine(JsonUtility.ToJson(stat));
        }

        public void Dispose() => writer.Dispose();
    }
}
