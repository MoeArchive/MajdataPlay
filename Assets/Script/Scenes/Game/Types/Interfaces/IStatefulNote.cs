﻿using MajdataPlay.Interfaces;
using MajdataPlay.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataPlay.Game.Types
{
    public interface IStatefulNote : IStateful<NoteStatus>
    {

    }
}