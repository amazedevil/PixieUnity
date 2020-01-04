using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Pixie.Unity
{
    public abstract class PXUnityMessageHandlerBase<T> : PXUnityMessageHandlerRawBase
    {
        public override Type DataType
        {
            get {
                return typeof(T);
            }
        }

        public T Data
        {
            get { return (T)data; }
        }

        public override void Execute() {
            if (!this.gameObject.activeInHierarchy) {
                return;
            }

            (this.GetType().GetField("messageReceivedEvent").GetValue(this) as UnityEvent<T>).Invoke(this.Data);
        }
    }
}