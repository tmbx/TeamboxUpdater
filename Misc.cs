/*
 * Misc.cs:
 *   Miscellaneous routines.
 *  
 * Author(s):
 *   François-Denis Gonthier
 * 
 * Copyright (C) 2010-2012 Opersys inc.
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; version 2
 * of the License, not any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
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
