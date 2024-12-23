using UnityEngine;
using System.Threading;
using System;
using System.Linq;
using UnityRawInput;
using System.Threading.Tasks;
using MajdataPlay.Extensions;
using MajdataPlay.Types;
using MajdataPlay.Utils;
using MychIO;
using Cysharp.Threading.Tasks;
using DeviceType = MajdataPlay.Types.DeviceType;
using MychIO.Device;
using System.Collections.Generic;
using MychIO.Event;
using MychIO.Connection;
using static UnityEngine.GraphicsBuffer;
using System.Runtime.CompilerServices;
//using Microsoft.Win32;
//using System.Windows.Forms;
//using Application = UnityEngine.Application;
//using System.Security.Policy;
#nullable enable
namespace MajdataPlay.IO
{
    public partial class InputManager : MonoBehaviour
    {
        public bool displayDebug = false;
        public bool useDummy = false;

        public event EventHandler<InputEventArgs>? OnAnyAreaTrigger;

        TimeSpan _btnDebounceTime = TimeSpan.Zero;
        TimeSpan _sensorDebounceTime = TimeSpan.Zero;
        bool[] _COMReport = Enumerable.Repeat(false,35).ToArray();
        Task? _recvTask = null;
        Mutex _buttonCheckerMutex = new();
        IOManager? _ioManager = null;
        CancellationTokenSource _cancelSource = new();

        void Awake()
        {
            MajInstances.InputManager = this;
            DontDestroyOnLoad(this);
            foreach (var (index, child) in transform.ToEnumerable().WithIndex())
            {
                var collider = child.GetComponent<Collider>();
                var type = (SensorType)index;
                _sensors[index] = child.GetComponent<Sensor>();
                _sensors[index].Type = type;
                _instanceID2SensorTypeMappingTable[collider.GetInstanceID()] = type;
                if(type.GetGroup() == SensorGroup.C)
                {
                    var childCollider = child.GetChild(0).GetComponent<Collider>();
                    _instanceID2SensorTypeMappingTable[childCollider.GetInstanceID()] = type;
                }
            }
            foreach(SensorType zone in Enum.GetValues(typeof(SensorType)))
            {
                if (((int)zone).InRange(0, 7))
                {
                    _btnLastTriggerTimes[zone] = DateTime.MinValue;
                }
                _sensorLastTriggerTimes[zone] = DateTime.MinValue;
            }
        }
        /// <summary>
        /// Used to check whether the device activation is caused by abnormal jitter
        /// </summary>
        /// <param name="zone"></param>
        /// <returns>
        /// If the trigger interval is lower than the debounce threshold, returns <see cref="bool">true</see>, otherwise <see cref="bool">false</see>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool JitterDetect(SensorType zone, DateTime now,bool isBtn = false)
        {
            DateTime lastTriggerTime;
            TimeSpan debounceTime;
            if(isBtn)
            {
                _btnLastTriggerTimes.TryGetValue(zone, out lastTriggerTime);
                debounceTime = _btnDebounceTime;
            }
            else
            {
                _sensorLastTriggerTimes.TryGetValue(zone, out lastTriggerTime);
                debounceTime = _sensorDebounceTime;
            }
            var diff = now - lastTriggerTime;
            if (diff < debounceTime)
            {
                Debug.Log($"[Debounce] Received {(isBtn?"button":"sensor")} response\nZone: {zone}\nInterval: {diff.Milliseconds}ms");
                return true;
            }
            return false;
        }
        void CheckEnvironment(bool forceQuit = true)
        {
            //// MSVC 2015-2019
            //var registryKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
            //using var key = Registry.LocalMachine.OpenSubKey(registryKey);
            //if(key is null)
            //{
            //    //var msg = "IO4 and HID input methods depend on the MSVC runtime library, but MajdataPlay did not find the MSVC runtime library on your computer. Please click \"OK\" to jump to download and install.";
            //    var msg = Localization.GetLocalizedText(MajText.MISSING_MSVC_CONTENT);
            //    if (string.IsNullOrEmpty(msg))
            //        msg = "MSVCRT not found\r\nClick \"OK\" to download";
            //    var title = "Missing MSVC";
            //    if (forceQuit)
            //    {
            //        MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        Application.OpenURL("https://aka.ms/vs/17/release/vc_redist.x64.exe");
            //        Application.Quit();
            //    }
            //    else
            //        Debug.LogWarning("Missing environment: MSVC runtime library not found.");
            //}
        }
        void Start()
        {
            switch(MajInstances.Setting.Misc.InputDevice.ButtonRing.Type)
            {
                case DeviceType.Keyboard:
                    CheckEnvironment(false);
                    StartInternalIOManager();
                    StartInternalIOListener();
                    break;
                case DeviceType.IO4:
                case DeviceType.HID:
                    CheckEnvironment();
                    StartExternalIOManager();
                    StartExternalIOListener();
                    break;
            }

        }
        void StartInternalIOManager()
        {
            _btnDebounceTime = TimeSpan.FromMilliseconds(MajInstances.Setting.Misc.InputDevice.ButtonRing.DebounceThresholdMs);
            _sensorDebounceTime = TimeSpan.FromMilliseconds(MajInstances.Setting.Misc.InputDevice.TouchPanel.DebounceThresholdMs);
            //RawInput.Start();
            //RawInput.OnKeyDown += OnRawKeyDown;
            //RawInput.OnKeyUp += OnRawKeyUp;
            try
            {
                COMReceiveAsync();
                RefreshKeyboardStateAsync();
            }
            catch
            {
                Debug.LogWarning("Cannot open COM3, using Mouse as fallback.");
                useDummy = true;
            }
        }
        public void StartExternalIOManager()
        {
            if(_ioManager is null)
                _ioManager = new();
            var useHID = MajInstances.Setting.Misc.InputDevice.ButtonRing.Type is DeviceType.HID;
            var executionQueue = GameManager.ExecutionQueue;
            var buttonRingCallbacks = new Dictionary<ButtonRingZone, Action<ButtonRingZone, InputState>>();
            var touchPanelCallbacks = new Dictionary<TouchPanelZone, Action<TouchPanelZone, InputState>>();

            foreach (ButtonRingZone zone in Enum.GetValues(typeof(ButtonRingZone)))
            {
                buttonRingCallbacks[zone] = (zone, state) =>
                {
                    var index = GetIndexByButtonRingZone(zone);
                    _buttonStates[index] = state is InputState.On;
                };
            }

            foreach (TouchPanelZone zone in Enum.GetValues(typeof(TouchPanelZone)))
            {
                touchPanelCallbacks[zone] = (zone, state) => _COMReport[(int)zone] = state is InputState.On;
            }

            
            _ioManager.Destroy();
            _ioManager.SubscribeToAllEvents(ExternalIOEventHandler);
            _ioManager.AddDeviceErrorHandler(new DeviceErrorHandler(_ioManager, 4));

            try
            {
                var deviceName = useHID ? AdxHIDButtonRing.GetDeviceName() : AdxIO4ButtonRing.GetDeviceName();
                var btnDebounce = MajInstances.Setting.Misc.InputDevice.ButtonRing.Debounce;
                var touchPanelDebounce = MajInstances.Setting.Misc.InputDevice.TouchPanel.Debounce;

                var btnProductId = MajInstances.Setting.Misc.InputDevice.ButtonRing.ProductId;
                var btnVendorId = MajInstances.Setting.Misc.InputDevice.ButtonRing.VendorId;
                var comPortNum = MajInstances.Setting.Misc.InputDevice.TouchPanel.COMPort;

                var btnPollingRate = MajInstances.Setting.Misc.InputDevice.ButtonRing.PollingRateMs;
                var btnDebounceThresholdMs = btnDebounce ? MajInstances.Setting.Misc.InputDevice.ButtonRing.DebounceThresholdMs : 0;

                var touchPanelPollingRate = MajInstances.Setting.Misc.InputDevice.TouchPanel.PollingRateMs;
                var touchPanelDebounceThresholdMs = touchPanelDebounce ? MajInstances.Setting.Misc.InputDevice.TouchPanel.DebounceThresholdMs : 0;

                var btnConnProperties = new Dictionary<string, dynamic>()
                {
                    { "PollingRateMs", btnPollingRate },
                    { "DebounceTimeMs", btnDebounceThresholdMs },
                    { "ProductId", btnProductId },
                    { "VendorId", btnVendorId },
                };
                var touchPanelConnProperties = new Dictionary<string, dynamic>()
                {
                    { "PollingRateMs", touchPanelPollingRate },
                    { "DebounceTimeMs", touchPanelDebounceThresholdMs },
                    { "ComPortNumber", comPortNum }
                };

                _ioManager.AddButtonRing(deviceName,
                                         inputSubscriptions: buttonRingCallbacks,
                                         connectionProperties: btnConnProperties);
                _ioManager.AddTouchPanel(AdxTouchPanel.GetDeviceName(),
                                         inputSubscriptions: touchPanelCallbacks,
                                         connectionProperties: touchPanelConnProperties);
                _ioManager.AddLedDevice(AdxLedDevice.GetDeviceName());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        void StartInternalIOListener()
        {
            UniTask.Void(async () =>
            {
                var executionQueue = GameManager.ExecutionQueue;
                while (!_cancelSource.IsCancellationRequested)
                {
                    try
                    {
                        if (useDummy)
                            UpdateMousePosition();
                        else
                            UpdateSensorState();
                        UpdateButtonState();
                        while (executionQueue.TryDequeue(out var eventAction))
                            eventAction();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
                }
            });
        }
        void StartExternalIOListener()
        {
            UniTask.Void(async () =>
            {
                var executionQueue = GameManager.ExecutionQueue;
                while (!_cancelSource.IsCancellationRequested)
                {
                    try
                    {
                        while (executionQueue.TryDequeue(out var eventAction))
                            eventAction();
                        UpdateSensorState();
                        UpdateButtonState();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
                }
            });
        }
        void ExternalIOEventHandler(IOEventType eventType,DeviceClassification deviceType,string msg)
        {
            var executionQueue = IOManager.ExecutionQueue;
            var logContent = $"From external IOManager:\nEventType: {eventType}\nDeviceType: {deviceType}\nMsg: {msg.Trim()}";
            switch (eventType)
            {
                case IOEventType.Attach:
                case IOEventType.Debug:
                    executionQueue.Enqueue(() => Debug.Log(logContent));
                    break;
                case IOEventType.ConnectionError:
                case IOEventType.SerialDeviceReadError:
                case IOEventType.HidDeviceReadError:
                case IOEventType.ReconnectionError:
                case IOEventType.InvalidDevicePropertyError:
                    executionQueue.Enqueue(() => Debug.LogError(logContent));
                    break;
                case IOEventType.Detach:
                    executionQueue.Enqueue(() => Debug.LogWarning(logContent));
                    break;
            }
        }
        void OnApplicationQuit()
        {
            _cancelSource.Cancel();
            if (_recvTask != null && !_recvTask.IsCompleted)
                _recvTask.Wait();
        }
        public void BindAnyArea(EventHandler<InputEventArgs> checker) => OnAnyAreaTrigger += checker;
        public void BindArea(EventHandler<InputEventArgs> checker, SensorType sType)
        {
            var sensor = GetSensor(sType);
            var button = GetButton(sType);
            if (sensor == null || button is null)
                throw new Exception($"{sType} Sensor or Button not found.");

            sensor.AddSubscriber(checker);
            button.AddSubscriber(checker);
        }
        public void UnbindAnyArea(EventHandler<InputEventArgs> checker) => OnAnyAreaTrigger -= checker;
        public void UnbindArea(EventHandler<InputEventArgs> checker, SensorType sType)
        {
            var sensor = GetSensor(sType);
            var button = GetButton(sType);
            if (sensor == null || button is null)
                throw new Exception($"{sType} Sensor or Button not found.");

            sensor.RemoveSubscriber(checker);
            button.RemoveSubscriber(checker);
        }
        public bool CheckAreaStatus(SensorType sType, SensorStatus targetStatus)
        {
            return CheckSensorStatus(sType,targetStatus) || CheckButtonStatus(sType, targetStatus);
        }
        public bool CheckSensorStatus(SensorType target, SensorStatus targetStatus)
        {
            var sensor = _sensors[(int)target];
            if (sensor == null)
                throw new Exception($"{target} Sensor or Button not found.");
            return sensor.Status == targetStatus;
        }
        public bool CheckButtonStatus(SensorType target, SensorStatus targetStatus)
        {
            if (target > SensorType.A8)
                throw new ArgumentOutOfRangeException("Button index cannot greater than A8");
            var button = GetButton(target);

            if (button is null)
                throw new Exception($"{target} Button not found.");

            return button.Status == targetStatus;
        }
        public void SetBusy(InputEventArgs args)
        {
            var type = args.Type;
            if (args.IsButton)
            {
                var button = GetButton(type);
                if (button is null)
                    throw new Exception($"{type} Button not found.");

                button.IsJudging = true;
            }
            else
            {
                var sensor = GetSensor(type);
                if (sensor is null)
                    throw new Exception($"{type} Sensor not found.");

                sensor.IsJudging = true;
            }
        }
        public void SetIdle(InputEventArgs args)
        {
            var type = args.Type;
            if (args.IsButton)
            {
                var button = GetButton(type);
                if (button is null)
                    throw new Exception($"{type} Button not found.");

                button.IsJudging = false;
            }
            else
            {
                var sensor = GetSensor(type);
                if (sensor is null)
                    throw new Exception($"{type} Sensor not found.");

                sensor.IsJudging = false;
            }
        }
        public bool IsIdle(InputEventArgs args)
        {
            bool isIdle;
            var type = args.Type;
            if (args.IsButton)
            {
                var button = GetButton(type);
                if (button is null)
                    throw new Exception($"{type} Button not found.");

                isIdle = !button.IsJudging;
            }
            else
            {
                var sensor = GetSensor(type);
                if (sensor is null)
                    throw new Exception($"{type} Sensor not found.");

                isIdle = !sensor.IsJudging;
            }
            return isIdle;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Button? GetButton(SensorType type)
        {
            var buttons = _buttons.AsSpan();
            return buttons.Find(x => x.Type == type);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sensor GetSensor(SensorType target) => _sensors[(int)target];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sensor[] GetSensors() => _sensors;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sensor[] GetSensors(SensorGroup group) => _sensors.Where(x => x.Group == group).ToArray();
        public void ClearAllSubscriber()
        {
            foreach(var sensor in _sensors.AsSpan())
                sensor.ClearSubscriber();
            foreach(var button in _buttons.AsSpan())
                button.ClearSubscriber();
            OnAnyAreaTrigger = null;
        }
        void PushEvent(InputEventArgs args)
        {
            if (OnAnyAreaTrigger is not null)
                OnAnyAreaTrigger(this, args);
        }
    }
}