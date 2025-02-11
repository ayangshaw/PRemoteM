﻿using System;
using System.IO;
using System.Threading.Tasks;
using _1Remote.Security;
using _1RM.Utils;
using _1RM.View.Utils;
using _1RM.Utils.WindowsSdk;
using _1RM.Utils.WindowsApi.Credential;
using _1RM.Utils.WindowsSdk.PasswordVaultManager;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Crypto.Signers;
using Microsoft.Win32;

namespace _1RM.Service
{
    public static class SecondaryVerificationHelper
    {
        private static bool? _isEnabled = null;
        private static DateTime _lastVerifyTime = DateTime.MinValue;

        public static async void Init()
        {
            var ret = await GetEnabled("SecondaryVerificationDisabled");
            if (ret == null)
            {
                SetEnabled(false);
            }
        }


        public static async void SetEnabled(bool enable)
        {
            var success = await SetEnabled(enable, "SecondaryVerificationDisabled");
            if (success)
                _isEnabled = enable;
        }


        public static async Task<bool> GetEnabled()
        {
            if (_isEnabled == null)
            {
                var ret = await GetEnabled("SecondaryVerificationDisabled");
                _isEnabled = ret != false;
            }
            return (bool)_isEnabled;
        }


        public static async Task<bool?> VerifyAsyncUi(bool defaultReturn = false, bool returnUntilOkOrCancel = true)
        {
            if (await GetEnabled() == false)
                return true;

            //if ((DateTime.Now - _lastVerifyTime).TotalSeconds < 30)
            //    return true;

            bool? result;
            bool widowsHelloIsOk = WindowsHelloHelper.IsOsSupported && await WindowsHelloHelper.HelloIsAvailable() == true;
            int counter = 0;
            MaskLayerController.ShowProcessingRing(IoC.Get<LanguageService>().Translate("Please complete the windows credentials verification"));
            while (true)
            {

                if (!widowsHelloIsOk)
                {
                    try
                    {

                        string title = IoC.Get<LanguageService>().Translate("Enter your credentials");
                        string message = "";
                        if (counter > 0)
                        {
                            message = IoC.Get<LanguageService>().Translate("Verification failed. Please try again.");
                        }
                        var ret = CredentialPrompt.LogonUserWithWindowsCredential(title, message,
                            null, // new WindowInteropHelper(this).Handle
                            null, null,
                            0
                            | (uint)CredentialPrompt.PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC
                            | (uint)CredentialPrompt.PromptForWindowsCredentialsFlag.CREDUIWIN_ENUMERATE_CURRENT_USER
                        );
                        if (ret == CredentialPrompt.LogonUserStatus.Success)
                        {
                            result = true;
                        }
                        else if (ret == CredentialPrompt.LogonUserStatus.Cancel)
                        {
                            result = null;
                        }
                        else
                        {
                            result = defaultReturn;
                        }
                    }
                    catch (Exception)
                    {
                        result = defaultReturn;
                    }
                }
                else
                {
                    result = await WindowsHelloHelper.HelloVerifyAsync();
                }

                if (result != false)
                    break;
                if (returnUntilOkOrCancel == false)
                    break;
                counter++;
            }

            MaskLayerController.HideMask();
            if (result == true)
                _lastVerifyTime = DateTime.Now;
            return result;
        }


        public static void VerifyAsyncUiCallBack(Action<bool?> callBack, bool defaultReturn = false, bool returnUntilOkOrCancel = true)
        {
            Task.Factory.StartNew(async () =>
            {
                bool? result = await VerifyAsyncUi(defaultReturn, returnUntilOkOrCancel);
                callBack.Invoke(result);
            });
        }





        private static async Task<bool> SetEnabled(bool enable, string key)
        {
            var success = false;
            var value = await DataProtectionForLocal.Protect("0") ?? "";

            if (enable == false)
            {
                string password = "";
                string username = "";
                if (value.Length > 1000)
                {
                    password = UnSafeStringEncipher.EncryptOnce("0");
                }
                else if (value.Length > 500)
                {
                    password = value.Substring(0, 500);
                    username = value.Substring(500);
                }
                success = Credential.Set($@"{Assert.APP_NAME}\{key}", username, password);
            }
            else
            {
                success = Credential.Set($@"{Assert.APP_NAME}\{key}", "", "");
            }


            if (!success || enable == true)
            {
                try
                {
                    var openSubKey = Registry.CurrentUser.OpenSubKey("Software", true);
                    var appNameKey = openSubKey?.CreateSubKey(Assert.APP_NAME);
                    if (appNameKey != null)
                    {
                        appNameKey.SetValue(key, enable == false ? value : "");
                        success = true;
                    }
                }
                catch (Exception)
                {
                    success = false;
                }
            }

            if (!success || enable == true)
            {
                try
                {
                    var pvm = new PasswordVaultManagerFileSystem(AppPathHelper.Instance.LocalityDirPath);
                    if (enable == false)
                    {
                        pvm.Add(key, value);
                        success = true;
                    }
                    else
                    {
                        pvm.Remove(key);
                    }
                }
                catch (Exception)
                {
                    success = false;
                }
            }
            return success;
        }
        private static async Task<bool?> GetEnabled(string key)
        {
            try
            {
                var c = Credential.Load($@"{Assert.APP_NAME}\{key}");
                if (c != null)
                {
                    if (await DataProtectionForLocal.Unprotect(c.Password + c.Username) == "0")
                    {
                        return false;
                    }
                    if (UnSafeStringEncipher.SimpleDecrypt(c.Password + c.Username) == "0")
                    {
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                var openSubKey = Registry.CurrentUser.OpenSubKey("Software", true);
                var appNameKey = openSubKey?.CreateSubKey(Assert.APP_NAME);
                if (appNameKey?.GetValue(key) is string txt)
                {
                    var value = await DataProtectionForLocal.Unprotect(txt);
                    return value != "0";
                }
            }
            catch (Exception)
            {
                // ignored
            }


            try
            {
                var pvm = new PasswordVaultManagerFileSystem(AppPathHelper.Instance.LocalityDirPath);
                if (pvm.Retrieve(key) is { } txt)
                {
                    var value = await DataProtectionForLocal.Unprotect(txt);
                    return value != "0";
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return null;
        }

    }
}
