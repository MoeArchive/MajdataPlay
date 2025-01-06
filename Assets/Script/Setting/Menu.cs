using MajdataPlay.Collections;
using MajdataPlay.Extensions;
using MajdataPlay.IO;
using MajdataPlay.Types;
using MajdataPlay.Utils;
using System;
using TMPro;
using UnityEngine;
using static UnityEngine.UI.Image;
#nullable enable
namespace MajdataPlay.Setting
{
    public class Menu : MonoBehaviour
    {
        public string Name { get; set; } = string.Empty;
        public int SelectedIndex => _selectedIndex;
        /// <summary>
        /// Option对象<para>e.g. GameSetting.Game</para>
        /// </summary>
        public object SubOptionObject { get; set; }
        public GameObject optionPrefab;

        float _lastWaitTime = 0;
        bool _isBound = false;
        Option[] _options = Array.Empty<Option>();
        SettingManager manager;
        void Start()
        {
            var type = SubOptionObject.GetType();
            var properties = type.GetProperties();
            _options = new Option[properties.Length];
            foreach(var (i,property) in properties.WithIndex())
            {
                var optionObj = Instantiate(optionPrefab, transform);
                var option = optionObj.GetComponent<Option>();
                _options[i] = option;
                option.PropertyInfo = property;
                option.OptionObject = SubOptionObject;
                option.Parent = this;
                option.Index = i;
            }
            var localizedText = Localization.GetLocalizedText(Name);
            titleText.text = localizedText;
            BindArea();
            manager = FindObjectOfType<SettingManager>();
        }
        void OnEnable()
        {
            BindArea();
        }
        void OnDisable()
        {
            _selectedIndex = 0;
            UnbindArea();
        }
        void OnDestroy()
        {
            UnbindArea();
        }
        void Update()
        {
            if(manager.IsPressed)
            {
                if (manager.PressTime < 0.7f)
                    return;
                else if (_lastWaitTime < 0.2f)
                {
                    _lastWaitTime += Time.deltaTime;
                    return;
                }
                switch(manager.Direction)
                {
                    case 1:
                        NextOption();
                        _lastWaitTime = 0;
                        break;
                    case -1:
                        PreviousOption();
                        _lastWaitTime = 0;
                        break;
                }
            }
            else
            {
                _lastWaitTime = 0;
            }
        }
        void OnLangChanged(object? sender, Language newLanguage)
        {
            var localizedText = Localization.GetLocalizedText(Name);
            titleText.text = localizedText;
        }
        void OnAreaDown(object sender, InputEventArgs e)
        {
            if (!e.IsClick)
                return;
            switch(e.Type)
            {
                case SensorType.A6:
                    PreviousOption();
                    break;
                case SensorType.A3:
                    NextOption();
                    break;
                default:
                    return;
            }
        }
        void PreviousOption()
        {
            _selectedIndex--;
            if (_selectedIndex < 0)
                manager.PreviousMenu();
            _selectedIndex = _selectedIndex.Clamp(0, _options.Length - 1);
        }
        void NextOption()
        {
            _selectedIndex++;
            if (_selectedIndex > _options.Length - 1)
                manager.NextMenu();
            _selectedIndex = _selectedIndex.Clamp(0, _options.Length - 1);
        }
        public void ToLast() => _selectedIndex = _options.Length - 1;
        public void ToFirst() => _selectedIndex = 0;
        void BindArea()
        {
            if (_isBound)
                return;
            _isBound = true;
            Localization.OnLanguageChanged += OnLangChanged;
            MajInstances.InputManager.BindButton(OnAreaDown, SensorType.A3);
            MajInstances.InputManager.BindButton(OnAreaDown, SensorType.A6);
        }
        void UnbindArea()
        {
            if (!_isBound)
                return;
            _isBound = false;
            Localization.OnLanguageChanged -= OnLangChanged;
            MajInstances.InputManager.UnbindButton(OnAreaDown, SensorType.A3);
            MajInstances.InputManager.UnbindButton(OnAreaDown, SensorType.A6);
        }
        [SerializeField]
        int _selectedIndex = 0;
        [SerializeField]
        TextMeshPro titleText;
    }
}
