﻿/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    public class AuditLogObject : IAuditLogObject
    {
        public AuditLogObject(object obj)
        {
            Object = obj;
        }

        public string AuditLogText
        {
            get
            {
                if (Object == null)
                    return LogMessage.MISSING;

                return AuditLogToStringHelper.InvariantToString(Object) ??
                       AuditLogToStringHelper.KnownTypeToString(Object) ??
                       Reflector.ToString(Object.GetType(), null, Object, true, false, 0, 0); // This will always return some non-null string representation
            }
        }

        public bool IsName
        {
            get { return false; }
        }

        public object Object { get; private set; }

        public static object GetObject(IAuditLogObject auditLogObj)
        {
            var obj = auditLogObj as AuditLogObject;
            return obj != null ? obj.Object : auditLogObj;
        }

        public static IAuditLogObject GetAuditLogObject(object obj)
        {
            bool usesReflection;
            return GetAuditLogObject(obj, out usesReflection);
        }

        public static IAuditLogObject GetAuditLogObject(object obj, out bool usesReflection)
        {
            var auditLogObj = obj as IAuditLogObject;
            usesReflection = auditLogObj == null && !Reflector.HasToString(obj) &&
                             !AuditLogToStringHelper.IsKnownType(obj);
            return auditLogObj ?? new AuditLogObject(obj);
        }
    }

    public class AuditLogPath : IAuditLogObject
    {
        private AuditLogPath(string path)
        {
            Path = path;
        }

        public static AuditLogPath Create(string path)
        {
            return string.IsNullOrEmpty(path)
                ? null
                : new AuditLogPath(path);
        }

        public string Path { get; private set; }

        public override string ToString()
        {
            return string.Format("{{4:{0}}}", Path);
        }

        public string AuditLogText
        {
            get { return ToString(); } // Not L10N
        }

        public bool IsName
        {
            get { return true; }
        }
    }
}