﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace Pixie.Unity
{
    public static class PXUnityMessageInfo
    {
        public const string MESSAGE_CLASS_FIELD_DATA_TYPE = "DataType";

        public const string MESSAGE_SERIALIZATION_FIELD_NAME = "message";
        public const string MESSAGE_SERIALIZATION_FIELD_BODY = "body";

        public static int GetMessageTypeHashCode(Type type) {
            return BitConverter.ToInt32(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(type.FullName)), 0);
        }
    }
}