using MajdataPlay.Utils;
using System.Runtime.CompilerServices;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Game
{
    public class OutlineLoader : MonoBehaviour
    {
        [SerializeField]
        Animator _effectAnim;

        bool _effectAvailable = false;
        GameManager _gameManager;
        SpriteRenderer _renderer;
        GamePlayManager _gpManager;
        void Awake()
        {
            MajInstanceHelper<OutlineLoader>.Instance = this;
        }
        void Start()
        {
            _gameManager = MajInstances.GameManager;
            _gpManager = MajInstanceHelper<GamePlayManager>.Instance!;
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = MajInstances.SkinManager.SelectedSkin.Outline;
            _effectAvailable = MajInstances.SkinManager.SelectedSkin.IsOutlineAvailable && _gpManager.IsClassicMode;
            if (_effectAvailable)
                SetColor();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Play()
        {
            if (!_effectAvailable)
                return;
            _effectAnim.SetTrigger("play");
        }
        void OnDestroy()
        {
            MajInstanceHelper<GamePlayManager>.Free();
        }
        void SetColor()
        {
            var outlineColor = Color.white;
            switch(_gameManager.SelectedDiff)
            {
                case Types.ChartLevel.Easy:
                    outlineColor = CreateColor(32, 63, 255);
                    break;
                case Types.ChartLevel.Basic:
                    outlineColor = CreateColor(75, 250, 65);
                    break;
                case Types.ChartLevel.Advance:
                    outlineColor = CreateColor(249, 230, 65);
                    break;
                case Types.ChartLevel.Expert:
                    outlineColor = CreateColor(255, 0, 0);
                    break;
                case Types.ChartLevel.Master:
                case Types.ChartLevel.ReMaster:
                    outlineColor = CreateColor(119, 0, 255);
                    break;
                case Types.ChartLevel.UTAGE:
                    outlineColor = CreateColor(255, 169, 218);
                    break;
            }
            _renderer.color = outlineColor;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Color CreateColor(int r,int g,int b,int a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Color CreateColor(int r, int g, int b)
        {
            return CreateColor(r, g, b, 255);
        }
    }
}