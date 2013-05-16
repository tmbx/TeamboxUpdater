/*
 * ProgressTray.cs:
 *   Tray icon manager.
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
