using MajdataPlay.Types;
using MajdataPlay.Utils;
using MajdataPlay.Extensions;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json.Serialization;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Scripting;// DO NOT REMOVE IT !!!
using MajdataPlay.Attributes;
using MajdataPlay.Net;
using System.Collections.Concurrent;
using MychIO;
using UnityEngine.Rendering;
using MajdataPlay.Timer;
using MajdataPlay.Collections;

namespace MajdataPlay
{
#nullable enable
    public class GameManager : MonoBehaviour
    {
        public static GameResult? LastGameResult { get; set; } = null;
        public static ConcurrentQueue<Action> ExecutionQueue { get; } = IOManager.ExecutionQueue;
        public static string AssestsPath { get; } = Path.Combine(Application.dataPath, "../");
        public static string ChartPath { get; } = Path.Combine(AssestsPath, "MaiCharts");
        public static string SettingPath { get; } = Path.Combine(AssestsPath, "settings.json");
        public static string SkinPath { get; } = Path.Combine(AssestsPath, "Skins");
        public static string CachePath { get; } = Path.Combine(AssestsPath, "Cache");
        public static string LogsPath { get; } = Path.Combine(AssestsPath, $"Logs");
        public static string LangPath { get; } = Path.Combine(Application.streamingAssetsPath, "Langs");
        public static string ScoreDBPath { get; } = Path.Combine(AssestsPath, "MajDatabase.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db");
        public static string LogPath { get; } = Path.Combine(LogsPath, $"MajPlayRuntime_{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.log");
        static CancellationTokenSource _globalCTS = new();
        public static CancellationToken GlobalCT { get; } = _globalCTS.Token;
        public static JsonSerializerOptions JsonReaderOption { get; } = new()
        {

            Converters =
            {
                new JsonStringEnumConverter()
            },
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };
        public GameSetting Setting
        {
            get => MajInstances.Setting;
            set => MajInstances.Setting = value;
        }
        /// <summary>
        /// Current difficult
        /// </summary>
        public ChartLevel SelectedDiff
        {
            get
            {
                return _selectedDiff;
            }
            set 
            {
                _selectedDiff = value;
            }
        }
        private ChartLevel _selectedDiff = ChartLevel.Easy;
        public int LastSettingPage { get; set; } = 0;

        //public bool isDanMode = false;
        //public int DanHP = 500;
        //public List<GameResult> DanResults = new();

        [SerializeField]
        TimerType _timer = MajTimeline.Timer;
        Task? _logWritebackTask = null;
        Queue<GameLog> _logQueue = new();

        

        void Awake()
        {
            //HttpTransporter.Timeout = TimeSpan.FromMilliseconds(10000);
#if !UNITY_EDITOR
            if(Process.GetProcessesByName("MajdataPlay").Length > 1)
            {
                Application.Quit();
            }
#endif
            Application.logMessageReceived += (c, trace, type) =>
            {
                _logQueue.Enqueue(new GameLog()
                {
                    Date = DateTime.Now,
                    Condition = c,
                    StackTrace = trace,
                    Type = type
                });
            };
            _logWritebackTask = LogWriteback();
            var s = "\n";
            s += $"################ MajdataPlay Startup Check ################\n";
            s += $"     OS       : {SystemInfo.operatingSystem}\n";
            s += $"     Model    : {SystemInfo.deviceModel} - {SystemInfo.deviceType}\n";
            s += $"     Processor: {SystemInfo.processorType}\n";
            s += $"     Memory   : {SystemInfo.systemMemorySize} MB\n";
            s += $"     Graphices: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB) - {SystemInfo.graphicsDeviceType}\n";
            s += $"################     Startup Check  End    ################";
            Debug.Log(s);
            Debug.Log($"Version: {MajInstances.GameVersion}");
            MajInstances.GameManager = this;
            _timer = MajTimeline.Timer;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            DontDestroyOnLoad(this);

            if (File.Exists(SettingPath))
            {
                var js = File.ReadAllText(SettingPath);
                GameSetting? setting;

                if (!Serializer.Json.TryDeserialize(js, out setting, JsonReaderOption) || setting is null)
                {
                    Setting = new();
                    Debug.LogError("Failed to read setting from file");
                }
                else
                {
                    Setting = setting;
                    //Reset Mod option after reboot
                    Setting.Mod = new ModOptions();
                }
            }
            else
            {
                Setting = new GameSetting();
                Save();
            }
            MajInstances.Setting = Setting;
            Setting.Misc.InputDevice.ButtonRing.PollingRateMs = Math.Max(0, Setting.Misc.InputDevice.ButtonRing.PollingRateMs);
            Setting.Misc.InputDevice.TouchPanel.PollingRateMs = Math.Max(0, Setting.Misc.InputDevice.TouchPanel.PollingRateMs);
            Setting.Misc.InputDevice.ButtonRing.DebounceThresholdMs = Math.Max(0, Setting.Misc.InputDevice.ButtonRing.DebounceThresholdMs);
            Setting.Misc.InputDevice.TouchPanel.DebounceThresholdMs = Math.Max(0, Setting.Misc.InputDevice.TouchPanel.DebounceThresholdMs);
            Setting.Display.InnerJudgeDistance = Setting.Display.InnerJudgeDistance.Clamp(0, 1);
            Setting.Display.OuterJudgeDistance = Setting.Display.OuterJudgeDistance.Clamp(0, 1);

            var fullScreen = Setting.Debug.FullScreen;
            Screen.fullScreen = fullScreen;

            var resolution = Setting.Display.Resolution.ToLower();
            if (resolution is not "auto")
            {
                var param = resolution.Split("x");
                int width, height;

                if (param.Length != 2)
                    return;
                else if (!int.TryParse(param[0], out width) || !int.TryParse(param[1], out height))
                    return;
                Screen.SetResolution(width, height, fullScreen);
            }
            var thiss = Process.GetCurrentProcess();
            thiss.PriorityClass = ProcessPriorityClass.RealTime;
            var availableLangs = Localization.Available;
            if (availableLangs.IsEmpty())
                return;
            var lang = availableLangs.Find(x => x.ToString() == Setting.Game.Language);
            if (lang is null)
            {
                lang = availableLangs.First();
                Setting.Game.Language = lang.ToString();
            }
            Localization.Current = lang;
        }
        void Start()
        {
            SelectedDiff = Setting.Misc.SelectedDiff;
            SongStorage.OrderBy = Setting.Misc.OrderBy;
        }
        void Update()
        {
            if(MajTimeline.Timer != _timer)
            {
                Debug.LogWarning($"Time provider changed:\nOld:{MajTimeline.Timer}\nNew:{_timer}");
                MajTimeline.Timer = _timer;
            }
        }
        private void OnApplicationQuit()
        {
            Save();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            _globalCTS.Cancel();
            foreach (var log in _logQueue)
                File.AppendAllText(LogPath, $"[{log.Date:yyyy-MM-dd HH:mm:ss}][{log.Type}] {log.Condition}\n{log.StackTrace}");
        }
        public void Save()
        {
            Setting.Misc.SelectedDiff = SelectedDiff;
            Setting.Misc.SelectedIndex = SongStorage.WorkingCollection.Index;
            Setting.Misc.SelectedDir = SongStorage.CollectionIndex;
            SongStorage.OrderBy.Keyword = string.Empty;
            Setting.Misc.OrderBy = SongStorage.OrderBy;

            var json = Serializer.Json.Serialize(Setting, JsonReaderOption);
            File.WriteAllText(SettingPath, json);
        }
        public void EnableGC()
        {
            if (!Setting.Debug.DisableGCInGameing)
                return;
#if !UNITY_EDITOR
            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
            Debug.LogWarning("GC has been enabled");
#endif
            GC.Collect();
        }
        public void DisableGC() 
        {
            if (!Setting.Debug.DisableGCInGameing)
                return;
            GC.Collect();
#if !UNITY_EDITOR
            GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
            Debug.LogWarning("GC has been disabled");
#endif
        }
        async Task LogWriteback()
        {
            var oldLogPath = Path.Combine(AssestsPath, "MajPlayRuntime.log");
            if (!Directory.Exists(LogsPath))
                Directory.CreateDirectory(LogsPath);
            if (File.Exists(oldLogPath))
                File.Delete(oldLogPath);
            if (File.Exists(LogPath))
                File.Delete(LogPath);
            while (true)
            {
                if (_logQueue.Count == 0)
                {
                    if (GlobalCT.IsCancellationRequested)
                        return;
                    await Task.Delay(50);
                    continue;
                }
                var log = _logQueue.Dequeue();
                await File.AppendAllTextAsync(LogPath, $"[{log.Date:yyyy-MM-dd HH:mm:ss.ffff}][{log.Type}] {log.Condition}\n{log.StackTrace}");
            }
        }
        class GameLog
        {
            public DateTime Date { get; set; }
            public string? Condition { get; set; }
            public string? StackTrace { get; set; }
            public LogType? Type { get; set; }
        }
    }
}