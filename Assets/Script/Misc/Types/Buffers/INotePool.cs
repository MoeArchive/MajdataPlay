﻿using MajdataPlay.Types;

namespace MajdataPlay.Buffers
{
    public interface INotePool<TInfo, TMember>: IObjectPool<IPoolableNote<TInfo, TMember>> 
        where TInfo : NotePoolingInfo where TMember : NoteQueueInfo
    {
        public void Update(float currentSec);
        public void Destroy();
    }
}
