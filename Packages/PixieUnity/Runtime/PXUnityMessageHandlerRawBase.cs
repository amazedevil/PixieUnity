using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pixie.Unity
{
    public class PXUnityMessageHandlerRawBase : MonoBehaviour
    {
        protected object data;

        public virtual Type DataType { get { return null; } }

        public virtual PXUnityMessageHandlerRawBase SetupData(object data) {
            this.data = data;
            return this;
        }

        public virtual void Execute() { }
    }
}