/*
 * Misc.cs:
 *   Miscellaneous routines.
 *  
 * Author(s):
 *   François-Denis Gonthier
 * 
 * Copyright (C) 2010-2012 Opersys inc.
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
 * 
 */

using System;

namespace TeamboxUpdater
{
    public delegate void EmptyDelegate();

    public static class Misc
    {
        public static bool LaterVersion(string vA, string vB)
        {
            bool isLater = false;
            string[] vA_parts = vA.Split(new char[] {'.'});
            string[] vB_parts = vB.Split(new char[] {'.'});

            for (int i = 0; i < vA_parts.Length; i++)
            {
                int vA_item = Int32.Parse(vA_parts[i]);
                int vB_item = Int32.Parse(vB_parts[i]);

                if (vA_item < vB_item)
                {
                    isLater = true;
                    break;
                }
            }

            if (vA_parts.Length < vB_parts.Length) isLater = true;

            return isLater;
        }
    }
}
