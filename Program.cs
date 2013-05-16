/*
 * Program.cs:
 *   Installer entry point and UI controller.
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
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using Timers = System.Timers;
using Microsoft.Win32;

namespace TeamboxUpdater
{
    public class TeamboxUpdater
    {      
        /// <summary>
        /// The main form of the program that display task progress.
        /// </summary>
        public ProgressForm ProgressForm;

        /// <summary>
        /// Tray icon.
        /// </summary>
        public ProgressTray ProgressTray;

        /// <summary>
        /// The list of all product.
        /// </summary>
        private List<Product> m_allProducts = new List<Product>();

        /// <summary>
        /// Queue of local products that we need to check for updates online.
        /// </summary>
        private List<Product> m_localProducts = new List<Product>();

        /// <summary>
        /// Queue of product we can update from the web.
        /// </summary>
        private List<Product> m_updateProducts = new List<Product>();

        /// <summary>
        /// Queue of product that were downloaded and installed.
        /// </summary>
        private List<Product> m_installableProducts = new List<Product>();

        /// <summary>
        /// Currently running asynchronous stask.
        /// </summary>
        private Task m_currentTask;

        /// <summary>
        /// This is set to true once an error has been raised.
        /// </summary>
        private bool m_gotError;

        /// <summary>
        /// Set to true once the balloon asking for the user to proceed to download
        /// has been shown.
        /// </summary>
        private bool m_downloadQueryPopped = false;

        /// <summary>
        /// Set to true once the balloon asking for the user to start the installers
        /// has been shown.
        /// </summary>
        private bool m_installQueryPopped = false;

        /// <summary>
        /// Poke the registry for all Teambox products that we can update. Return
        /// a list of products that can be updated.
        /// </summary>
        private List<Product> GetProductsInfo()
        {
            string baseKey = @"Software\Teambox";
            Dictionary<string, Product> prods = new Dictionary<string, Product>();
            RegistryKey key = Registry.LocalMachine.OpenSubKey(baseKey);
            string[] products;

            if (key == null) return new List<Product>(prods.Values);

            products = key.GetSubKeyNames();

            foreach (string pname in products)
            {
                RegistryKey productKey = key.OpenSubKey(pname);

                // Get the update URL.
                object productUrlObject = productKey.GetValue("URL");
                object productIncludesObject = productKey.GetValue("Includes");
                object productVersionObject = productKey.GetValue("InstallVersion");
                if (productUrlObject == null || productVersionObject == null) continue;

                if (productIncludesObject != null)
                {
                    // If this product includes other products, mark the other products
                    // has IncludedElsewhere.
                    string[] productIncludes = ((string)productIncludesObject).Split(new char[] {','});

                    foreach (string prodInc in productIncludes)
                    {
                        if (prods.ContainsKey(prodInc))
                        {
                            if (!prods.ContainsKey(prodInc)) prods[prodInc] = new Product();
                            prods[prodInc].IncludedElsewhere = true;
                        }
                    }
                }

                // Add the product to the list of manageable products.
                if (!prods.ContainsKey(pname)) prods[pname] = new Product();
                prods[pname].Name = pname;
                prods[pname].URL = (string)productUrlObject;
                prods[pname].LocalVersion = (string)productVersionObject;
            }

            List<Product> updateProducts = new List<Product>();

            // Create a list that removes the product included elsewhere.
            foreach (Product p in prods.Values) if (!p.IncludedElsewhere) updateProducts.Add(p);

            return updateProducts;
        }

        /// <summary>
        /// Timer programmed to automatically close the application if the user
        /// doesn't manifests itself.
        /// </summary>
        private Timers.Timer m_delayCloseTimer;

        /// <summary>
        /// Program the timer to close the application.
        /// </summary>
        private void DelayClose(int seconds)
        {
            if (m_delayCloseTimer == null)
            {
                // Close the application after a little while.
                m_delayCloseTimer = new Timers.Timer(seconds * 1000.0);
                m_delayCloseTimer.Elapsed += new Timers.ElapsedEventHandler(OnDelayedClose);
                m_delayCloseTimer.Start();
            }
        }

        /// <summary>
        /// Cancel the timer running to close the application.
        /// </summary>
        private void CancelClose()
        {
            if (m_delayCloseTimer != null) m_delayCloseTimer.Stop();
            m_delayCloseTimer = null;
        }
        /// <summary>
        /// Called when an installer is done running.
        /// </summary>
        private void OnInstallDone(object sender, TaskActionEventArgs ev)
        {
            Task evTask = sender as Task;

            m_currentTask = null;

            // Run the next task.
            RunCurrentStep();
        }

        /// <summary>
        /// Called when an installer is finished downloading
        /// </summary>
        private void OnWebDownloadDone(object sender, TaskActionEventArgs ev)
        {
            Task evTask = sender as Task;

            if (ev.Partial)
            {
                string statusText = "Downloading update ... " + ev.ProgressPct + "%";

                ProgressForm.Status = statusText;
                ProgressTray.Status = statusText;

                // If this is a partial result, update the progress form with
                // a new % of progress.
                ProgressForm.Progress = ev.ProgressPct;
            }
            else
            {
                // Add the product to the queue of installable product.s
                m_installableProducts.Add(evTask.TaskProduct);

                m_currentTask = null;

                // Run the next task.
                RunCurrentStep();
            }
        }

        /// <summary>
        /// Called when a product update data has been fetched online.
        /// </summary>
        private void OnWebCheckDone(object sender, TaskActionEventArgs ev)
        {
            Task evTask = sender as Task;

            // We don't care about partial results.
            if (ev.Partial)
            {
                // Just update the progress bar.
                ProgressForm.Progress = ev.ProgressPct;
            }
            else
            {
                // Compare the version.
                if (Misc.LaterVersion(evTask.TaskProduct.LocalVersion, evTask.TaskProduct.WebVersion))
                {
                    m_updateProducts.Add(evTask.TaskProduct);
                }

                m_currentTask = null;

                RunCurrentStep();
            }
        }

        private void OnTaskError(object sender, TaskErrorEventArgs ev)
        {
            string statusText;

            // Dump the stacktrace of the exception in a file so it may useful
            // to debug problems.
            if (ev.Exception != null)
            {
                string logPath = Path.Combine(Path.GetTempPath(), "TeamboxUpdater.log");
                FileStream exFile = null;
                StreamWriter exStream = null;

                try
                {
                    exFile = new FileStream(logPath, FileMode.Create, FileAccess.Write);
                    exStream = new StreamWriter(exFile);
                    exStream.WriteLine(ev.Exception.ToString());
                }
                catch (Exception) { }
                finally
                {
                    if (exStream != null) exStream.Flush();
                    if (exFile != null) exFile.Close();
                }
            }

            // If webcheck fails, this may be because of a lack of connectivity. Just
            // show a warning and don't bother the user.
            if (m_currentTask is WebCheck)
            {
                statusText = "Could not check for updates. Will retry later.";
                ProgressTray.Warning(
                    "Teambox Update", statusText,
                    5);
                ProgressTray.Status = statusText;
                ProgressForm.Status = statusText;
                DelayClose(10);
                return;
            }

            // If the download or the installed failed to run, show an error message.

            if (m_currentTask is WebDownload)
            {
                statusText = "Failed to download update for " + m_currentTask.TaskProduct.Name + ".";
                ProgressTray.Error("Teambox Update", statusText, 10);
                ProgressTray.Status = statusText;
                ProgressForm.Status = statusText;
            }

            if (m_currentTask is Installer)
            {
                statusText = "Error while installing update for " + m_currentTask.TaskProduct.Name + ".";
                ProgressTray.Error("Teambox Update", statusText, 10);
                ProgressTray.Status = statusText;
                ProgressForm.Status = statusText;
            }

            m_gotError = true;
            m_currentTask = null;

            DelayClose(30);
        }

        /// <summary>
        /// Start the installer for one updatable product.
        /// </summary>
        private void InstallProduct()
        {
            string statusText;
            Product installProduct = m_installableProducts[0];
            m_installableProducts.RemoveAt(0);

            statusText = "Installing update for " + installProduct.Name + ".";
            ProgressForm.Status = statusText;
            ProgressForm.Status = statusText;

            m_currentTask = new Installer(installProduct);
            m_currentTask.TaskDone += new TaskActionEventHandler(OnInstallDone);
            m_currentTask.TaskError += new TaskErrorEventHandler(OnTaskError);
            m_currentTask.Start();

            // Disable the cancel button.
            ProgressForm.Cancellable = false;
        }

        /// <summary>
        /// Download the update for one updatable product.
        /// </summary>
        private void WebDownload()
        {
            string statusText;
            Product webProduct = m_updateProducts[0];

            m_updateProducts.RemoveAt(0);

            statusText = "Downloading update for " + webProduct.Name + ".";
            ProgressForm.Status = statusText;
            ProgressTray.Status = statusText;

            m_currentTask = new WebDownload(webProduct);
            m_currentTask.TaskDone += new TaskActionEventHandler(OnWebDownloadDone);
            m_currentTask.TaskError += new TaskErrorEventHandler(OnTaskError);
            m_currentTask.Start();
        }

        /// <summary>
        /// Check for the update of one installed product.
        /// </summary>
        private void WebCheckProduct()
        {
            string statusText;
            Product webCheckProduct = m_localProducts[0];

            m_localProducts.RemoveAt(0);

            statusText = "Checking for installed product updates online.";
            ProgressForm.Status = statusText;
            ProgressTray.Status = statusText;

            m_currentTask = new WebCheck(webCheckProduct);
            m_currentTask.TaskDone += new TaskActionEventHandler(OnWebCheckDone);
            m_currentTask.TaskError += new TaskErrorEventHandler(OnTaskError);
            m_currentTask.Start();
        }
        
        /// <summary>
        /// Display a bubble next to the tray icon, asking if the user wants to start
        /// the download of product updates.
        /// </summary>
        private void QueryDownload()
        {
            m_downloadQueryPopped = true;

            if (m_updateProducts.Count == 1)
            {
                Product wp = m_updateProducts[0];
                ProgressTray.Message(
                    "Teambox Update",
                    "An update for " + wp.Name + " is available." +
                    " Click here to start the upgrade process.",
                    15);
            }
            else if (m_updateProducts.Count > 1)
            {
                ProgressTray.Message(
                    "Teambox Update",
                    "Updates for several Teambox products are available. " +
                    " Click here to start the upgrade process.",
                    15);
            }

            DelayClose(30);
        }

        /// <summary>
        /// Display a bubble next to the tray icon, asking if the user wants to start
        /// the installer for product updates.
        /// </summary>
        private void QueryInstall()
        {
            m_installQueryPopped = true;

            if (m_installableProducts.Count == 1)
            {
                Product wp = m_installableProducts[0];
                ProgressTray.Message(
                    "Teambox Update",
                    "Update for " + wp.Name + " was downloaded." +
                    " Click here to install the update.",
                    15);
            }
            else if (m_installableProducts.Count > 1)
            {
                ProgressTray.Message(
                    "Teambox Update",
                    "Updates for several Teambox products were downloaded. " +
                    " Click here to install the update.",
                    15);
            }

            DelayClose(30);
        }

        /// <summary>
        /// Display a message saying we are finished and program the application
        /// to close automatically.
        /// </summary>
        private void DoneInstall()
        {
            string statusText = "Done.";

            ProgressForm.Status = statusText;
            ProgressTray.Status = statusText;

            DelayClose(30);
        }
        
        /// <summary>
        /// Run the next asynchroneous task depending on the state of this
        /// object.
        /// </summary>
        private void RunCurrentStep()
        {
            // First state, check for updates online.
            if (m_localProducts.Count > 0) WebCheckProduct();

            // Next state, query the user if he wants to download what is updatable.
            else if (m_updateProducts.Count > 0 && !m_downloadQueryPopped) QueryDownload();

            // Fire the download.
            else if (m_updateProducts.Count > 0 && m_downloadQueryPopped) WebDownload();

            // Next state, query the user if he wants to install what was downloaded.
            else if (m_installableProducts.Count > 0 && !m_installQueryPopped) QueryInstall();

            // Final state, install each products that was downloaded.
            else if (m_installableProducts.Count > 0 && m_installQueryPopped) InstallProduct();

            // Done installing everything.
            else if (m_installableProducts.Count == 0) DoneInstall();
        }

        /// <summary>
        /// Toggle the visibility of the progress form.
        /// </summary>
        private void OnProgressTrayClick(object sender, EventArgs ev)
        {
            ProgressForm.ToggleVisible();
        }

        /// <summary>
        /// Called when the user clicks on the bubble next to the tray icon.
        /// </summary>
        private void OnProgressTrayMessageAcked(object sender, EventArgs ev)
        {
            if (m_gotError) return;

            // Cancel the timer that automatically closes the application.
            CancelClose();

            // Move to the next step.
            RunCurrentStep();
        }

        /// <summary>
        /// Called when the quit timer exits without any user actions.
        /// </summary>
        private void OnDelayedClose(object sender, Timers.ElapsedEventArgs ev)
        {
            ProgressTray.Close();
            ProgressForm.Close();

            Cleanup();

            Application.Exit();
        }

        /// <summary>
        /// Called when the 'Cancel' button is clicked on the progress form.
        /// </summary>
        private void OnCancel(object sender, EventArgs ev)
        {
            // Cancel all operations and prepare to automatically quit.
            if (m_currentTask != null) m_currentTask.Cancel();
            ProgressForm.Status = "Update cancelled. Bye.";
            ProgressForm.Cancellable = true;
            DelayClose(30);
        }

        /// <summary>
        /// Delete all the downloaded installers.
        /// </summary>
        private void Cleanup()
        {
            foreach (Product p in m_allProducts)
            {
                if (p.InstallerPath != null && File.Exists(p.InstallerPath))
                {
                    File.Delete(p.InstallerPath);
                }
                p.InstallerPath = null;
            }
        }

        public TeamboxUpdater()
        {
            ProgressTray = new ProgressTray();
            ProgressForm = new ProgressForm();

            ProgressTray.ProgressTrayClick += new EventHandler(OnProgressTrayClick);
            ProgressTray.ProgressTrayMessageAcked += new EventHandler(OnProgressTrayMessageAcked);

            ProgressForm.Cancelled += new EventHandler(OnCancel);

            m_localProducts = GetProductsInfo();
            m_allProducts.AddRange(m_localProducts);

            // Nothing to update? Die out.
            if (m_localProducts.Count == 0) DelayClose(30);

            // No network connectivity? Die out.
            else if (!SystemInformation.Network) DelayClose(30);

            // Start the update tasks.
            else RunCurrentStep();
        }
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TeamboxUpdater tbxUpdater = new TeamboxUpdater();

            Application.Run();
        }
    }
}