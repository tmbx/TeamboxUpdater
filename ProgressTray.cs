/*
 * ProgressTray.cs:
 *   Tray icon manager.
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
using System.Windows.Forms;

namespace TeamboxUpdater
{
    public delegate void ProgressTrayMessageShown();

    public class ProgressTray
    {
        private NotifyIcon m_trayIcon;

        /// <summary>
        /// Called when the user clicks on the update balloon.
        /// </summary>
        public event EventHandler ProgressTrayMessageAcked;

        /// <summary>
        /// Called when the tray icon is clicked.
        /// </summary>
        public event EventHandler ProgressTrayClick;

        /// <summary>
        /// Display a balloon near the tray icon.
        /// </summary>
        public void Message(string title, string message, int seconds)
        {
            m_trayIcon.BalloonTipText = message;
            m_trayIcon.BalloonTipTitle = title;
            m_trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            m_trayIcon.ShowBalloonTip(seconds * 1000);
        }

        public void Error(string title, string message, int seconds)
        {
            m_trayIcon.BalloonTipText = message;
            m_trayIcon.BalloonTipTitle = title;            
            m_trayIcon.BalloonTipIcon = ToolTipIcon.Error;
            m_trayIcon.ShowBalloonTip(seconds * 1000);
        }

        public void Warning(string title, string message, int seconds)
        {
            m_trayIcon.BalloonTipText = message;
            m_trayIcon.BalloonTipTitle = title;
            m_trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
            m_trayIcon.ShowBalloonTip(seconds * 1000);
        }

        /// <summary>
        /// Closes the tray icon.
        /// </summary>
        public void Close()
        {
            m_trayIcon.Visible = false;
            m_trayIcon.Dispose();
        }

        /// <summary>
        /// Changes the tray tooltip.
        /// </summary>
        public string Status
        {
            set
            {
                m_trayIcon.Text = value;
            }
        }

        public ProgressTray()
        {
            m_trayIcon = new NotifyIcon();
            m_trayIcon.Icon = Properties.Resources.TrayIcon;
            m_trayIcon.Visible = true;

            m_trayIcon.BalloonTipClicked += new EventHandler(OnBalloonTipClicked);
            m_trayIcon.Click += new EventHandler(OnClick);
        }

        /// <summary>
        /// Forwards click to the caller.
        /// </summary>
        private void OnClick(object sender, EventArgs ev)
        {
            if (ProgressTrayClick != null) ProgressTrayClick(sender, ev);
        }

        /// <summary>
        /// Forwards balloon tooltip click to the caller.
        /// </summary>
        private void OnBalloonTipClicked(object sender, EventArgs ev)
        {
            if (ProgressTrayMessageAcked != null) ProgressTrayMessageAcked(sender, ev);            
        }
    }
}
