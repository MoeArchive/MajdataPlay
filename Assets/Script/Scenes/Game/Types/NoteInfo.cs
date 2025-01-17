﻿using MajdataPlay.Buffers;
using MajdataPlay.Interfaces;
using MajdataPlay.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
#nullable enable
namespace MajdataPlay.Game.Types
{
    public unsafe class NoteInfo : ComponentInfo<IStateful<NoteStatus>>
    {
        public override bool IsFixedUpdatable => _fixedUpdate is not null;
        public override bool IsUpdatable => _update is not null;
        public override bool IsLateUpdatable => _lateUpdate is not null;
        public bool IsValid => _update is not null ||
                               _fixedUpdate is not null ||
                               _lateUpdate is not null;
        public NoteStatus State => Object?.State ?? NoteStatus.Destroyed;


        delegate void ComponentMethod();
        ComponentMethod? _update = null;
        ComponentMethod? _fixedUpdate = null;
        ComponentMethod? _lateUpdate = null;

        IUpdatableComponent<NoteStatus>? _updatableComponent = null;
        IFixedUpdatableComponent<NoteStatus>? _fixedUpdatableComponent = null;
        ILateUpdatableComponent<NoteStatus>? _lateUpdatableComponent = null;
        public NoteInfo(IStateful<NoteStatus> noteObj)
        {
            if (noteObj is IUpdatableComponent<NoteStatus> component)
            {
                _updatableComponent = component;
                _update = new ComponentMethod(component.ComponentUpdate);
            }
            if (noteObj is IFixedUpdatableComponent<NoteStatus> _component)
            {
                _fixedUpdatableComponent = _component;
                _fixedUpdate = new ComponentMethod(_component.ComponentFixedUpdate);
            }
            if (noteObj is ILateUpdatableComponent<NoteStatus> __component)
            {
                _lateUpdatableComponent = __component;
                _lateUpdate = new ComponentMethod(__component.ComponentLateUpdate);
            }
            Object = noteObj;
        }
        public override void Update()
        {
            if (_update is not null)
            {
                if (CanExecute())
                    _update();
            }
        }
        public override void LateUpdate()
        {
            if (_lateUpdate is not null)
            {
                if (CanExecute())
                    _lateUpdate();
            }
        }
        public override void FixedUpdate()
        {
            if (_fixedUpdate is not null)
            {
                if (CanExecute())
                    _fixedUpdate();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanExecute()
        {
            return State is not (NoteStatus.Start or NoteStatus.Destroyed) &&
                   ((_updatableComponent?.Active ?? _fixedUpdatableComponent?.Active ?? _lateUpdatableComponent?.Active) ?? false);
        }
    }
}
