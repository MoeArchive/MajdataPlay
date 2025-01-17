﻿using MajdataPlay.Collections;
using MajdataPlay.Extensions;
using MajdataPlay.Game.Controllers;
using MajdataPlay.Game.Types;
using MajdataPlay.Game.Utils;
using MajdataPlay.IO;
using MajdataPlay.Types;
using MajdataPlay.Attributes;
using MajdataPlay.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#nullable enable
namespace MajdataPlay.Game.Notes
{
    public sealed class SlideDrop : SlideBase,IConnectableSlide, IEndableNote
    {
        public bool IsMirror 
        { 
            get => _isMirror; 
            set => _isMirror = value; 
        }
        public bool IsSpecialFlip 
        { 
            get => _isSpecialFlip; 
            set => _isSpecialFlip = value; 
        } // fixes known star problem

        readonly List<Vector3> _slidePositions = new();
        readonly List<Quaternion> _slideRotations = new();

        SpriteRenderer _starRenderer;
        SlideTable _table;

        protected override void Awake()
        {
            base.Awake();
            _noteChecker = new(Check);
        }
        /// <summary>
        /// Slide初始化
        /// </summary>
        public override void Initialize()
        {
            if (State >= NoteStatus.PreInitialized)
                return;
            State = NoteStatus.PreInitialized;
            var star = _stars[0];
            var slideTable = SlideTables.FindTableByName(_slideType);

            if (slideTable is null)
                throw new MissingComponentException($"Slide table of \"{_slideType}\" is not found");
            else if (star is null)
                throw new MissingComponentException("Slide star not found");

            _table = slideTable;

            _slideOK = transform.GetChild(transform.childCount - 1).gameObject; //slideok is the last one
            _slideOKAnim = _slideOK.GetComponent<Animator>();
            _slideOKController = _slideOK.GetComponent<LoadJustSprite>();

            _starTransforms = new Transform[1];
            _starTransforms[0] = star.transform;
            _starRenderer = star.GetComponent<SpriteRenderer>();
            _slideBars = new GameObject[transform.childCount - 1];
            _slideBarTransforms = new Transform[transform.childCount - 1];
            _slideBarRenderers = new SpriteRenderer[transform.childCount - 1];

            for (var i = 0; i < Transform.childCount - 1; i++)
            {
                _slideBars[i] = Transform.GetChild(i).gameObject;
                _slideBarRenderers[i] = _slideBars[i].GetComponent<SpriteRenderer>();
                _slideBarTransforms[i] = _slideBars[i].transform;
            }


            if (_isMirror)
            {
                _table.Mirror();
                Transform.localScale = new Vector3(-1f, 1f, 1f);
                Transform.rotation = Quaternion.Euler(0f, 0f, -45f * StartPos);
                _slideOK.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                Transform.rotation = Quaternion.Euler(0f, 0f, -45f * (StartPos - 1));
            }

            var diff = Math.Abs(1 - StartPos);
            if(diff != 0)
                _table.Diff(diff);

            LoadPath();
            LoadSkin();
            SetActive(false);
            SetStarActive(false);
            // 计算Slide淡入时机
            // 在8.0速时应当提前300ms显示Slide
            _fadeInTiming = -3.926913f / Speed;
            _fadeInTiming += _gameSetting.Game.SlideFadeInOffset;
            _fadeInTiming += _startTiming;
            // Slide完全淡入时机
            // 正常情况下应为负值；速度过高将忽略淡入
            _fullFadeInTiming = _fadeInTiming + 0.2f;
            //var interval = fullFadeInTiming - fadeInTiming;
            //fadeInAnimator = GetComponent<Animator>();
            //Destroy(GetComponent<Animator>());
            //淡入时机与正解帧间隔小于200ms时，加快淡入动画的播放速度
            //fadeInAnimator.speed = 0.2f / interval;
            //fadeInAnimator.SetTrigger("slide");
            SetSlideBarAlpha(0f);
            _starTransforms[0].position = _slidePositions[0];
            _starTransforms[0].transform.localScale = new Vector3(0f, 0f, 1f);
            _judgeQueues[0] = _table.JudgeQueue;

            if (ConnectInfo.IsConnSlide && ConnectInfo.IsGroupPartEnd)
                _judgeQueues[0].LastOrDefault().SetIsLast();
            else if (ConnectInfo.IsConnSlide)
                _judgeQueues[0].LastOrDefault().SetNonLast();
        }
        void UpdateJudgeQueue()
        {
            var judgeQueue = _judgeQueues[0];
            if (ConnectInfo.TotalJudgeQueueLen < 4)
            {
                if (ConnectInfo.IsGroupPartHead)
                {
                    judgeQueue[0].CanSkip = true;
                    judgeQueue[1].CanSkip = false;
                }
                else if (ConnectInfo.IsGroupPartEnd)
                {
                    judgeQueue[0].CanSkip = false;
                    judgeQueue[1].CanSkip = true;
                }
            }
            else
            {
                foreach (var judgeArea in judgeQueue)
                    judgeArea.CanSkip = true;
            }
        }
        public float GetSlideLength()
        {
            float len = 0;
            for (int i = 0; i < _slidePositions.Count - 2; i++)
            {
                var a = _slidePositions[i];
                var b = _slidePositions[i + 1];
                len += (b - a).magnitude;
            }
            return len;
        }
        void Start()
        {
            Initialize();
            if (ConnectInfo.IsConnSlide)
            {
                Length = ConnectInfo.TotalLength / ConnectInfo.TotalSlideLen * GetSlideLength();
                if (!ConnectInfo.IsGroupPartHead)
                {
                    if (Parent is null)
                        throw new NullReferenceException();
                    var parent = Parent.GameObject.GetComponent<SlideDrop>();
                    Timing = parent.Timing + parent.Length;
                }
                UpdateJudgeQueue();
            }

            if(ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide)
            {
                var percent = _table.Const;
                _judgeTiming = Timing + Length * (1 - percent);
                _lastWaitTime = Length *  percent;
            }

            _judgeAreas = _table.JudgeQueue.SelectMany(x => x.GetSensorTypes())
                                           .GroupBy(x => x)
                                           .Select(x => x.Key)
                                           .ToArray();

            FadeIn().Forget();
        }
        public override void ComponentFixedUpdate()
        {
            /// time      是Slide启动的时间点
            /// timeStart 是Slide完全显示但未启动
            /// LastFor   是Slide的时值
            //var timing = _gpManager.AudioTime - _timing;
            var startTiming = _gpManager.AudioTime - _startTiming;
            var tooLateTiming = _timing + _length + 0.6 + MathF.Min(_gameSetting.Judge.JudgeOffset , 0);
            var isTooLate = _gpManager.AudioTime - tooLateTiming >= 0;

            if (!_canCheck)
            {
                if (ConnectInfo.IsGroupPart)
                {
                    if (ConnectInfo.IsGroupPartHead && startTiming >= -0.05f)
                        _canCheck = true;
                    else if (!ConnectInfo.IsGroupPartHead)
                        _canCheck = ConnectInfo.ParentFinished || ConnectInfo.ParentPendingFinish;
                }
                else if (startTiming >= -0.05f)
                    _canCheck = true;
            }

            var canJudge = ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide;

            if(canJudge)
            {
                if(!_isJudged)
                {
                    if (IsFinished)
                    {
                        HideAllBar();
                        if(IsClassic)
                            Judge_Classic(_gpManager.ThisFrameSec);
                        else
                            Judge(_gpManager.ThisFrameSec);
                        return;
                    }
                    else if(isTooLate)
                        TooLateJudge();
                }
                else
                {
                    if (_lastWaitTime < 0)
                        End();
                    else
                        _lastWaitTime -= Time.fixedDeltaTime;
                }
            }
        }
        public override void ComponentUpdate()
        {
            // ConnSlide
            var star = _stars[0];
            var starTransform = _starTransforms[0];
            if (_stars.IsEmpty() || star is null)
            {
                if (IsFinished)
                {
                    End();
                    return;
                }
                Check();
                return;
            }
            else if(_isArrived)
            {
                Check();
                return;
            }

            if(!_isStarActive)
            {
                SetStarActive(true);
                _isStarActive = true;
            }
            var timing = CurrentSec - Timing;
            if (timing <= 0f)
            {
                float alpha;
                if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartHead)
                    alpha = 0;
                else
                {
                    // 只有当它是一个起点Slide（而非Slide Group中的子部分）的时候，才会有开始的星星渐入动画
                    alpha = 1f - -timing / (_timing - _startTiming);
                    alpha = alpha > 1f ? 1f : alpha;
                    alpha = alpha < 0f ? 0f : alpha;
                }

                _starRenderer.color = new Color(1, 1, 1, alpha);
                starTransform.localScale = new Vector3(alpha + 0.5f, alpha + 0.5f, alpha + 0.5f);
                starTransform.position = _slidePositions[0];
                ApplyStarRotation(_slideRotations[0]);
            }
            else
            {
                StarUpdate();
            }
            Check();
        }        
        /// <summary>
        /// 判定队列检查
        /// </summary>
        void Check()
        {
            if (IsDestroyed || !IsInitialized)
                return;
            else if (IsFinished || !_canCheck)
                return;
            else if (_isChecking)
                return;
            else if (_gpManager.IsAutoplay)
                return;
            var queue = _judgeQueues[0];
            _isChecking = true;


            var first = queue[0];
            var fType = first.GetSensorTypes();
            var canPlaySFX = ConnectInfo.IsGroupPartHead || !ConnectInfo.IsConnSlide;
            JudgeArea? second = null;

            if (queue.Length >= 2)
                second = queue[1];
            
            foreach (var t in fType)
            {
                var sensor = _ioManager.GetSensor(t);
                first.Judge(t, sensor.Status);
            }

            if(canPlaySFX && first.On)
                PlaySFX();

            if (second is not null && (first.CanSkip || first.On))
            {
                var sType = second.GetSensorTypes();
                foreach (var t in sType)
                {
                    var sensor = _ioManager.GetSensor(t);
                    second.Judge(t, sensor.Status);
                }

                if (second.IsFinished)
                {
                    HideBar(first.SlideIndex);
                    _judgeQueues[0] = queue.Skip(2).ToArray();
                    _isChecking = false;
                    SetParentFinish();
                    return;
                }
                else if (second.On)
                {
                    HideBar(first.SlideIndex);
                    _judgeQueues[0] = queue.Skip(1).ToArray();
                    _isChecking = false;
                    SetParentFinish();
                    return;
                }
            }

            if (first.IsFinished)
            {
                HideBar(first.SlideIndex);
                _judgeQueues[0] = queue.Skip(1).ToArray();
                _isChecking = false;
                SetParentFinish();
                return;
            }
            _isChecking = false;
        }
        void SetParentFinish()
        {
            if (Parent is not null)
            {
                if(_judgeQueues[0].Length < _table.JudgeQueue.Length && !ConnectInfo.ParentFinished)
                    Parent.ForceFinish();
            }
        }
        protected override void TooLateJudge()
        {
            if (_isJudged)
            {
                End();
                return;
            }
            base.TooLateJudge();
            End();
        }
        public override void End(bool forceEnd = false)
        {
            if (IsDestroyed)
                return;
            State = NoteStatus.Destroyed;
            foreach (var sensor in ArrayHelper.ToEnumerable(_judgeAreas))
                _ioManager.UnbindSensor(_noteChecker, sensor);
            base.End();
            if (forceEnd)
            {
                Destroy(_slideOK);
                Destroy(gameObject);
                return;
            }
            

            if (ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide)
            {
                ConvertJudgeResult(ref _judgeResult);
                JudgeResultCorrection(ref _judgeResult);
                var result = new JudgeResult()
                {
                    Grade = _judgeResult,
                    Diff = _judgeDiff,
                    IsEX = IsEX,
                    IsBreak = IsBreak
                };
                // 只有组内最后一个Slide完成 才会显示判定条并增加总数
                _objectCounter.ReportResult(this, result);
                if(PlaySlideOK(result))
                {
                    if (IsClassic)
                    {
                        _slideOKAnim.SetTrigger("classic");
                    }
                    else if (IsBreak && _judgeResult == JudgeGrade.Perfect)
                    {
                        _slideOKAnim.runtimeAnimatorController = MajInstances.SkinManager.JustBreak;
                    }
                    _slideOKController.SetResult(_judgeResult);
                }
                
                PlayJudgeSFX(result);
                //PlaySlideOK(result);
            }
            //else
            //    Destroy(_slideOK);
            // Destroy(gameObject);
            //SetActive(false);
        }
        /// <summary>
        /// 更新引导Star状态
        /// <para>包括位置，角度</para>
        /// </summary>
        void StarUpdate()
        {
            var starTransform = _starTransforms[0];

            _starRenderer.color = Color.white;
            starTransform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var process = MathF.Min((Length - GetRemainingTimeWithoutOffset()) / Length, 1);
            var indexProcess = (_slidePositions.Count - 1) * process;
            var index = (int)indexProcess;
            var pos = indexProcess - index;

            if (process == 1)
            {
                starTransform.position = _slidePositions[_slidePositions.Count - 1];
                ApplyStarRotation(_slideRotations[_slideRotations.Count - 1]);
                if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartEnd)
                    DestroyStars();
                _isArrived = true;
            }
            else
            {
                var a = _slidePositions[index + 1];
                var b = _slidePositions[index];
                var ba = a - b;
                var newPos = ba * pos + b;

                starTransform.position = newPos;
                if (index < _slideRotations.Count - 1)
                {
                    var _a = _slideRotations[index + 1].eulerAngles.z;
                    var _b = _slideRotations[index].eulerAngles.z;
                    var dAngle = Mathf.DeltaAngle(_b, _a) * pos;
                    dAngle = Mathf.Abs(dAngle);
                    var newRotation = Quaternion.Euler(0f, 0f,
                                    Mathf.MoveTowardsAngle(_b, _a, dAngle));
                    ApplyStarRotation(newRotation);
                }
            }

            if(_gpManager.IsAutoplay)
            {
                var queue = _judgeQueues?[0];
                var canPlaySFX = ConnectInfo.IsGroupPartHead || !ConnectInfo.IsConnSlide;
                if (queue is null || queue.IsEmpty())
                    return;
                else if(process >= 1)
                {
                    HideAllBar();
                    var autoplayParam = _gpManager.AutoplayParam;
                    if (autoplayParam.InRange(0, 14))
                        _judgeResult = (JudgeGrade)autoplayParam;
                    else
                        _judgeResult = (JudgeGrade)_randomizer.Next(0, 15);
                    _isJudged = true;
                    _lastWaitTime = 0;
                    _judgeDiff = _judgeResult switch
                    {
                        < JudgeGrade.Perfect => 1,
                        > JudgeGrade.Perfect => -1,
                        _ => 0
                    };
                    return;
                }
                else if(process > 0 && canPlaySFX)
                    PlaySFX();
                var areaIndex = (int)(process * queue.Length) - 1;
                if (areaIndex < 0)
                    return;
                var barIndex = queue[areaIndex].SlideIndex;
                HideBar(barIndex);
            }
        }
        void ApplyStarRotation(Quaternion newRotation)
        {
            var star = _stars[0];
            var starTransform = _starTransforms[0];
            if (star is null)
                return;
            var halfFlip = newRotation.eulerAngles;

            halfFlip.z += 180f;
            if (_isSpecialFlip)
                starTransform.rotation = Quaternion.Euler(halfFlip);
            else
                starTransform.rotation = newRotation;
            //starTransform.rotation = newRotation;
        }
        void LoadPath()
        {
            _slidePositions.Add(GetPositionFromDistance(4.8f));
            for (int i = 0; i < _slideBars.Length; i++)
            {
                var bar = _slideBars[i];
                _slidePositions.Add(bar.transform.position);

                _slideRotations.Add(Quaternion.Euler(bar.transform.rotation.normalized.eulerAngles + new Vector3(0f, 0f, 18f)));
                if(i == _slideBars.Length - 1)
                {
                    var a = _slideBars[i - 1].transform.rotation.normalized.eulerAngles;
                    var b = bar.transform.rotation.normalized.eulerAngles;
                    var diff = a - b;
                    var newEulerAugle = b - diff;
                    _slideRotations.Add(Quaternion.Euler(newEulerAugle + new Vector3(0f, 0f, 18f)));
                    //if(diff.z != 0)
                    //{
                    //    var _a = _slideBars[i - 1].transform.position;
                    //    var _b = bar.transform.position;
                    //    var m = (_a - _b).magnitude;
                    //    var _c = m / Mathf.Tan(Mathf.Deg2Rad * 5.625f);
                    //    var d = GetPositionFromDistance(4.8f, _endPos);

                    //    var _m = (_b - d).magnitude;
                    //    var angle = Mathf.Atan(_m / _c) * Mathf.Rad2Deg;
                    //    var newEulerAugle = new Vector3(0, 0, angle);
                    //    var magicNum = angle / 5.625f * 18f;
                    //    if (diff.z < 0) 
                    //    {
                    //        newEulerAugle = new Vector3(0, 0, -angle);
                    //    }
                    //    newEulerAugle = b - newEulerAugle;
                    //    _slideRotations.Add(Quaternion.Euler(newEulerAugle + new Vector3(0f, 0f, magicNum)));
                    //}
                    //else
                    //{
                    //    _slideRotations.Add(Quaternion.Euler(bar.transform.rotation.normalized.eulerAngles + new Vector3(0f, 0f, 18f)));
                    //}
                }
            }
            var endPos = GetPositionFromDistance(4.8f, _endPos);
            _slidePositions.Add(endPos);
            //_slideRotations.Add(_slideRotations.LastOrDefault());
        }
        protected override void LoadSkin()
        {
            var bars = _slideBars;
            var skin = MajInstances.SkinManager.GetSlideSkin();
            var star = _stars[0]!;
            var barSprite = skin.Normal;
            var starSprite = skin.Star.Normal;
            Material? breakMaterial = null;

            if(IsEach)
            {
                barSprite = skin.Each;
                starSprite = skin.Star.Each;
            }
            if(IsBreak)
            {
                barSprite = skin.Break;
                starSprite = skin.Star.Break;
                breakMaterial = BreakMaterial;
            }

            foreach(var bar in bars)
            {
                var barRenderer = bar.GetComponent<SpriteRenderer>();
                
                barRenderer.color = new Color(1f, 1f, 1f, 0f);
                barRenderer.sortingOrder = _sortOrder--;
                barRenderer.sortingLayerName = "Slides";

                barRenderer.sprite = barSprite;
                

                if(breakMaterial is not null)
                {
                    barRenderer.sharedMaterial = breakMaterial;
                    //var controller = bar.AddComponent<BreakShineController>();
                    //controller.Parent = this;
                }
            }

            var starRenderer = star.GetComponent<SpriteRenderer>();
            starRenderer.sprite = starSprite;
            if (breakMaterial is not null)
            {
                starRenderer.sharedMaterial = breakMaterial;
            }

            if (_isJustR)
            {
                if (_slideOK.GetComponent<LoadJustSprite>().SetR() == 1 && _isMirror)
                {
                    _slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                    var angel = _slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                    _slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
                }
            }
            else
            {
                if (_slideOK.GetComponent<LoadJustSprite>().SetL() == 1 && !_isMirror)
                {
                    _slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                    var angel = _slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                    _slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
                }
            }

            _slideOK.SetActive(false);
            _slideOK.transform.SetParent(transform.parent);
        }
        protected override void Check(object sender, InputEventArgs arg) => Check();

        [ReadOnlyField]
        [SerializeField]
        bool _isMirror = false;
        [ReadOnlyField]
        [SerializeField]
        bool _isSpecialFlip = false;
    }
}