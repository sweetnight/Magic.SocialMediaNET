using Magic.BrowserAutomationNET;
using OpenQA.Selenium;
using System.Diagnostics;

namespace Magic.SocialMediaNET
{
    public class WhatsApp
    {

        public Chrome Chrome { get; set; }
        public int TimeoutMs { get; set; } = 15000;

        public WhatsApp(Chrome chrome, int timeoutMs = 15000)
        {
            Chrome = chrome;
            TimeoutMs = timeoutMs;
        } // end of method

        public bool InjectWAPI(string jsPath, out string message)
        {
            if (!File.Exists(jsPath))
            {
                message = "File WAPI tidak ditemukan.";
                return false;
            }

            string jsCode = File.ReadAllText(jsPath);
            return Chrome.InjectScript(jsCode, out message);
        } // end of method

        public bool WaitForWebpackReady(int timeoutMs = 15000, int intervalMs = 500)
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)Chrome.Driver!;
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                try
                {
                    var result = js.ExecuteScript("return typeof window.webpackChunkwhatsapp_web_client !== 'undefined';");
                    if (result is bool b && b)
                        return true;
                }
                catch { }

                Thread.Sleep(intervalMs);
                elapsed += intervalMs;
            }

            return false;
        } // end of method

        public bool WaitForWAPIReady(int timeoutMs = 20000, int intervalMs = 500)
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)Chrome.Driver!;
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                try
                {
                    var result = js.ExecuteScript(@"
                return (typeof window !== 'undefined'
                        && typeof window.WAPI !== 'undefined'
                        && typeof window.WAPI.sendMessage === 'function'
                        && typeof window.Store !== 'undefined'
                        && typeof window.Store.Chat !== 'undefined');
            ");
                    if (result is bool b && b)
                        return true;
                }
                catch { }

                Thread.Sleep(intervalMs);
                elapsed += intervalMs;
            }

            return false;
        }


        public bool SendMessage(string nomorWa, string pesan, out string response)
        {
            response = "";

            if (nomorWa.StartsWith("0"))
                nomorWa = "62" + nomorWa.Substring(1);
            else if (nomorWa.StartsWith("+"))
                nomorWa = nomorWa.Substring(1);

            string chatId = nomorWa + "@c.us";
            string script = $@"return await WAPI.sendMessage('{chatId}', `{pesan}`);";

            try
            {
                var result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteScript(script);
                response = result?.ToString() ?? "OK";
                return true;
            }
            catch (Exception ex)
            {
                response = "Gagal kirim pesan. Exception: " + ex.Message;
                return false;
            }
        }

        public bool SendImage(string nomorWa, string base64Image, string fileName, string caption, out string response)
        {
            response = "";

            if (nomorWa.StartsWith("0"))
                nomorWa = "62" + nomorWa.Substring(1);
            else if (nomorWa.StartsWith("+"))
                nomorWa = nomorWa.Substring(1);

            string chatId = nomorWa + "@c.us";
            string script = $@"return await WAPI.sendImage('{base64Image}', '{chatId}', '{fileName}', `{caption}`);";

            try
            {
                var result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteScript(script);
                response = result?.ToString() ?? "OK";
                return true;
            }
            catch (Exception ex)
            {
                response = "Gagal kirim gambar. Exception: " + ex.Message;
                return false;
            }
        }

        public bool ReplyMessage(string nomorWa, string messageId, string message, out string response)
        {
            response = "";

            if (nomorWa.StartsWith("0"))
                nomorWa = "62" + nomorWa.Substring(1);
            else if (nomorWa.StartsWith("+"))
                nomorWa = nomorWa.Substring(1);

            string chatId = nomorWa + "@c.us";
            string script = $@"return await WAPI.reply('{chatId}', `{message}`, '{messageId}');";

            try
            {
                var result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteScript(script);
                response = result?.ToString() ?? "OK";
                return true;
            }
            catch (Exception ex)
            {
                response = "Gagal reply pesan. Exception: " + ex.Message;
                return false;
            }
        }

    } // end of class
} // end of namespace
