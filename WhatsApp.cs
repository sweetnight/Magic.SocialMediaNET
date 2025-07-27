using Magic.BrowserAutomationNET;
using OpenQA.Selenium;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Magic.SocialMediaNET
{

    public class WhatsApp
    {

        public enum EventState
        {

            OpeningWhatsAppWeb,
            ChatStillLoading,
            StartScriptInjection,
            StartSendingMessage,
            StartSendingImage,
            StartCheckingNumber,
            StartGetAllGroupIdentities,
            StartGetAllGroupDetails,
            StartGetParticipants,
            StartGetPastParticipants,
            GetGroupDetailsIteration,
            DestinationNumberHasWhatsApp,
            DestinationNumberHasNoWhatsApp,
            WaitingForFilepath

        } // end of enum

        public event Action<WhatsAppEventArgs>? WhatsAppEvents;

        public class WhatsAppEventArgs
        {

            public EventState WhatsAppEventState { get; set; }
            public int GroupsAmount { get; set; }
            public int GroupProcessIteration { get; set; }

            public WhatsAppEventArgs(EventState whatsAppEventState)
            {

                WhatsAppEventState = whatsAppEventState;

            } // end of method

            public WhatsAppEventArgs(EventState whatsAppEventState, int groupsAmount, int groupProcessIteration)
            {

                WhatsAppEventState = whatsAppEventState;
                GroupsAmount = groupsAmount;
                GroupProcessIteration = groupProcessIteration;

            } // end of method
        
        }

        public enum ConnectionStatus
        {

            NotConnected,
            Connected,
            NotChecked

        } // end of enum

        public enum FailReason
        {
            WPPFileIsNull,
            WPPFileIsNotFound,
            SomehowFailToInject,
            WPPInjectedScriptDoesntExist,
            NoWhatsAppOnDestinationNumber,
            SomehowFailSendMessage,
            SomehowFailSendImage,
            ImageFileDoesntExist,
            JavascriptError,
            LoggedOut,
            UnknownError,
            ReadByteFail
        } // end of enum

        public Chrome Chrome { get; set; }
        public string? JsPath { get; set; }
        public int Timeout { get; set; }

        public WhatsApp(Chrome chrome, string? jsPath = null, int timeout = 60)
        {

            Chrome = chrome;
            JsPath = jsPath;
            Timeout = timeout;

        } // end of method

        public ConnectionStatus CheckConnection()
        {

            WebPage webPage = Chrome.GetCurrentUrl();

            if (!webPage.Url.Contains("web.whatsapp.com"))
            {
                WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.OpeningWhatsAppWeb));
                Chrome.Navigate("https://web.whatsapp.com");
            }

            Magic.BrowserAutomationNET.WebElement profileButton = new BrowserAutomationNET.WebElement();

            bool eventFired = false;

            // 600 --> 10 menit, karena ada Thread.Sleep(1000) per loop
            for(int i = 0; i < 600; i++)
            {
                profileButton = Chrome.FindElementByXPath($"//header//button[{Chrome.ToLower("@aria-label")}='profile']|//header//button[{Chrome.ToLower("@aria-label")}='profil']|//div[{Chrome.ToLower("normalize-space(.)")}='steps to log in']|//div[{Chrome.ToLower("normalize-space(.)")}='langkah untuk login']|//div[contains({Chrome.ToLower("text()")}, 'loading your chats')]", Timeout);

                if (!profileButton.State) return ConnectionStatus.NotConnected;

                string? innerText = profileButton.SafeGetAttribute("innerText");


                if(!string.IsNullOrEmpty(innerText))
                {
                    innerText = innerText.ToLower();

                    if (innerText.Contains("loading your chats"))
                    {
                        if (!eventFired)
                        {
                            eventFired = true;
                            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.ChatStillLoading));
                        }

                        Debug.WriteLine("WhatsApp ==================== : Chat masih loading.");
                        Thread.Sleep(1000);
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
                
            }

            string elementFound = profileButton.Item!.TagName;

            if (elementFound == "button") return ConnectionStatus.Connected;

            return ConnectionStatus.NotConnected;

        } // end of method

        public bool WPPConnectSafeInject(out string message, out FailReason? failReason)
        {

            bool isInjected = IsWPPConnectReady(timeout: 1);

            if (isInjected)
            {
                message = "WPPConnect already injected.";
                failReason = null;
                return true;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartScriptInjection));

            if(JsPath == null)
            {
                message = "WPPConnect file is not defined and null.";
                failReason = FailReason.WPPFileIsNull;
                return false;
            }

            if (!File.Exists(JsPath))
            {
                message = "WPPConnect file is not found.";
                failReason = FailReason.WPPFileIsNotFound;
                return false;
            }

            string jsCode = File.ReadAllText(JsPath);

            try
            {
                Chrome.InjectScript(jsCode, out message);
            }
            catch (Exception ex)
            {
                message = $"Error inject WPPConnect: {ex.Message}";
                failReason = FailReason.SomehowFailToInject;
                return false;
            }

            isInjected = IsWPPConnectReady();

            if (isInjected)
            {
                message = "WPPConnect is injected successfully.";
                failReason = null;
                return true;
            }
            else
            {
                message = "Fail inject WPPConnect.";
                failReason = FailReason.WPPInjectedScriptDoesntExist;
                return false;
            }

        } // end of method

        public bool IsWPPConnectReady(int timeout = 15, int interval = 1)
        {

            bool isReady = false;

            for (int i = 0; i < timeout; i++)
            {
                try
                {
                    object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteScript(@"
                        return !!window.WPP && WPP.isReady;
                    ");
                    isReady = result != null && (bool)result;
                }
                catch
                {
                    // abaikan error sementara
                }

                if (isReady)
                {
                    Debug.WriteLine("WhatsApp ==================== : WPPConnect siap, jalankan action...");
                    return true;
                }

                Thread.Sleep(interval * 1000);
            }

            if (!isReady)
            {
                Debug.WriteLine("WhatsApp ==================== : WPPConnect belum siap.");
                return false;
            }

            return true;

        } // end of method

        public bool SendMessage(string destinationNumber, string message, out string response, out FailReason? failReason)
        {
            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                response = "Text message is FAIL to be sent because WPP Script can't be injected.";
                return false;
            }

            string filteredPhone = FilterPhone(destinationNumber);

            bool isWAExists = IsWAExists(filteredPhone, out _, out failReason);

            if (!isWAExists)
            {
                response = "Destination number DOES NOT have active WhatsApp service.";
                failReason = FailReason.NoWhatsAppOnDestinationNumber;
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartSendingMessage));

            message = message
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n");

            string jsSend = @$"
                return WPP.chat
                    .find('{filteredPhone}@c.us')
                    .then(() => 
                        WPP.chat.sendTextMessage('{filteredPhone}@c.us', '{message}', {{
                            createChat: true
                        }})
                    )
                    .then(() => 'success')
                    .catch(e => 'error: ' + e.message);
            ";

            bool injectResult = Chrome.InjectScript(jsSend, out string jsResult);
            response = jsResult;

            if (!injectResult)
            {
                response = "Script execution failed or returned no result.";
                failReason = FailReason.SomehowFailSendMessage;
                return false;
            }

            if (jsResult.StartsWith("success"))
            {
                response = "Text message is sent successfully.";
                failReason = null;
                return true;
            }
            else if (
                jsResult.Contains("Not logged in") ||
                jsResult.Contains("Cannot read properties") ||
                jsResult.Contains("Cannot read properties of undefined") ||
                jsResult.Contains("Cannot read properties of null")
            )
            {
                response = "WhatsApp Web seems to be logged out or unavailable.";
                failReason = FailReason.LoggedOut;
                return false;
            }
            else
            {
                response = "Text message failed to be sent: " + jsResult;
                failReason = FailReason.SomehowFailSendMessage;
                return false;
            }
        }


        public bool SendImage(string destinationNumber, string imageFilename, string caption, out string response, out FailReason? failReason)
        {

            response = "";
            
            // CEK APAKAH SUDAH INJECT? INJECT JIKA BELUM

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                response = "Image message is FAIL to be sent because WPP Script can't be injected.";
                return false;
            }

            // CEK FILE GAMBAR

            if (!File.Exists(imageFilename))
            {
                response = "Image file does not exist.";
                failReason = FailReason.ImageFileDoesntExist;
                return false;
            }

            string filteredPhone = FilterPhone(destinationNumber);

            bool isWAExists = IsWAExists(filteredPhone, out _, out failReason);

            if (!isWAExists)
            {
                response = "Destination number DOES NOT have active WhatsApp service.";
                failReason = FailReason.NoWhatsAppOnDestinationNumber;
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartSendingImage));

            // Convert file to base64
            string base64Data = "";
            string mimeType = "";

            try
            {
                byte[] imageBytes = File.ReadAllBytes(imageFilename);
                base64Data = Convert.ToBase64String(imageBytes);

                string extension = Path.GetExtension(imageFilename).TrimStart('.').ToLower();

                mimeType = extension switch
                {
                    "jpg" or "jpeg" => "image/jpeg",
                    "png" => "image/png",
                    "gif" => "image/gif",
                    "bmp" => "image/bmp",
                    "webp" => "image/webp",
                    _ => "application/octet-stream" // fallback
                };
            }
            catch (Exception ex)
            {
                response = "Failed to read or process image file: " + ex.Message;
                failReason = FailReason.ReadByteFail;
                return false;
            }

            // Escape caption untuk JS
            caption = caption
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n");

            string jsSend = @$"
                return WPP.chat
                    .find('{filteredPhone}@c.us')
                    .then(() =>
                        WPP.chat.sendFileMessage(
                            '{filteredPhone}@c.us',
                            'data:{mimeType};base64,{base64Data}',
                            {{
                                type: 'image',
                                caption: '{caption}',
                                filename: '{Path.GetFileName(imageFilename)}'
                            }}
                        )
                    )
                    .then(() => 'success')
                    .catch(e => 'error: ' + e.message);
            ";

            object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteScript(jsSend);
            string jsResult = result?.ToString() ?? "";

            if (jsResult.StartsWith("success"))
            {
                response = "Image message is sent successfully.";
                failReason = null;
                return true;
            }
            else if (
                jsResult.Contains("Cannot read properties") ||
                jsResult.Contains("Not logged in") ||
                jsResult.Contains("Cannot read properties of undefined") ||
                jsResult.Contains("Cannot read properties of null")
            )
            {
                response = "WhatsApp Web seems to be logged out or unavailable.";
                failReason = FailReason.LoggedOut;
                return false;
            }
            else
            {
                response = "Image sending failed: " + jsResult;
                failReason = FailReason.SomehowFailSendImage;
                return false;
            }

        } // end of method

        public bool IsWAExists(string destinationNumber, out string response, out FailReason? failReason)
        {

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                response = "Check number is FAIL because WPP Script can't be injected.";
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartCheckingNumber));

            // Pastikan nomor diformat dengan benar
            string chatId = FilterPhone(destinationNumber.Trim()) + "@c.us";

            // JS code untuk diinject
            string jsCode = $@"
                const callback = arguments[arguments.length - 1];
                WPP.contact.queryExists('{chatId}')
                  .then(r => {{
                      console.log('queryExists result:', r);
                      if (r && r.wid) {{
                          callback(true);
                      }} else {{
                          callback(false);
                      }}
                  }})
                  .catch(err => {{
                      console.error('queryExists error:', err);
                      callback(false);
                  }});
            ";

            try
            {
                object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteAsyncScript(jsCode);

                bool resultBool = Convert.ToBoolean(result);

                if (resultBool)
                {
                    response = "Destination number has active WhatsApp service.";
                    Debug.WriteLine("WhatsApp ==================== : " + response);
                    WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.DestinationNumberHasWhatsApp));
                }
                else
                {
                    response = "Destination number DOES NOT have active WhatsApp service.";
                    Debug.WriteLine("WhatsApp ==================== : " + response);
                    WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.DestinationNumberHasNoWhatsApp));
                }

                return resultBool;
            }
            catch (Exception ex)
            {
                response = "Error checking WA existence: " + ex.Message;
                return false;
            }

        } // end of method

        public bool GetAllGroupIdentities(out string response, out FailReason? failReason)
        {

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                response = "Check number is FAIL because WPP Script can't be injected.";
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartGetAllGroupIdentities));

            // Ambil hanya: id, name, isLocked (isAnnounceGrpRestrict)
            string jsCode = $@"
                const callback = arguments[arguments.length - 1];
                WPP.group.getAllGroups()
                    .then(groups => {{
                        const filtered = groups
                            .filter(g => g && typeof g.name === 'string' && g.name.trim().length > 0)
                            .map(g => {{
                                return {{
                                    groupJid: g.id && g.id._serialized ? g.id._serialized : g.id,
                                    name: g.name,
                                    isLocked: g.isAnnounceGrpRestrict || false
                                }};
                            }});
                        callback(JSON.stringify(filtered));
                    }})
                    .catch(err => callback({{ error: err.toString() }}));
            ";

            try
            {
                object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteAsyncScript(jsCode);

                if (result is IDictionary<string, object> err && err.ContainsKey("error"))
                {
                    response = "JavaScript error: " + err["error"];
                    failReason = FailReason.JavascriptError;
                    return false;
                }

                response = result?.ToString() ?? "[]";

                Debug.WriteLine("Filtered group JSON:");
                Debug.WriteLine(response);

                return true;
            }
            catch (OpenQA.Selenium.JavaScriptException jsEx)
            {
                response = "JavaScript error: " + jsEx.Message;
                Debug.WriteLine(response);
                failReason = FailReason.JavascriptError;
                return false;
            }
            catch (Exception ex)
            {
                response = "General error: " + ex.Message;
                failReason = FailReason.UnknownError;
                return false;
            }

        } // end of method

        public bool GetAllGroupDetails(string jsonIdentities, out string resultJson, out FailReason? failReason)
        {
            failReason = null;
            resultJson = "[]";

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                resultJson = "Check number is FAIL because WPP Script can't be injected.";
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartGetAllGroupDetails));

            try
            {
                // Parse input JSON dari GetAllGroupIdentities
                List<Dictionary<string, object>>? groups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonIdentities);

                if (groups == null || groups.Count == 0)
                {
                    resultJson = "No group identities found.";
                    return false;
                }

                int totalGroups = groups.Count;
                List<Dictionary<string, object>> detailedGroups = new List<Dictionary<string, object>>();

                for (int i = 0; i < totalGroups; i++)
                {
                    var group = groups[i];
                    string? groupJid = group.ContainsKey("groupJid") ? group["groupJid"]?.ToString() : null;

                    if (string.IsNullOrWhiteSpace(groupJid))
                        continue;

                    string jsScript = $@"
                        const callback = arguments[arguments.length - 1];
                        (async () => {{
                            try {{
                                const metadata = await WPP.group.getParticipants('{groupJid}');
                                const isAdmin = await WPP.group.iAmAdmin('{groupJid}');
                                callback({{
                                    membersAmount: metadata.length,
                                    isAdmin: isAdmin
                                }});
                            }} catch (e) {{
                                callback({{ error: e.toString() }});
                            }}
                        }})()
                    ";

                    object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteAsyncScript(jsScript);

                    if (result is IDictionary<string, object> err && err.ContainsKey("error"))
                    {
                        Debug.WriteLine($"Failed group {groupJid}: " + err["error"]);
                        continue; // Skip group with error
                    }

                    // Tambahkan data baru ke objek group
                    var updatedGroup = new Dictionary<string, object>(group);

                    if (result is IDictionary<string, object> detail)
                    {
                        if (detail.TryGetValue("membersAmount", out var members))
                            updatedGroup["membersAmount"] = Convert.ToInt32(members);
                        if (detail.TryGetValue("isAdmin", out var admin))
                            updatedGroup["isAdmin"] = Convert.ToBoolean(admin);
                    }

                    detailedGroups.Add(updatedGroup);

                    // Invoke progress event di setiap iterasi, iterasi mulai dari 1
                    WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.GetGroupDetailsIteration, totalGroups, i + 1));
                }

                // Serialize hasil akhir
                resultJson = JsonSerializer.Serialize(detailedGroups, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return true;
            }
            catch (Exception ex)
            {
                resultJson = "Error: " + ex.Message;
                failReason = FailReason.UnknownError;
                return false;
            }
        } // end of method

        public bool GetParticipants(string groupJid, out string response, out FailReason? failReason)
        {
            failReason = null;

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);

            if (!isWPPInjected)
            {
                response = "GetParticipants FAILED because WPP Script can't be injected.";
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartGetParticipants));

            Debug.WriteLine($@"WhatsApp ==================== GROUP JID: {groupJid}");

            string jsCode = $@"
                const callback = arguments[arguments.length - 1];
                WPP.group.getParticipants('{groupJid}')
                    .then(participants => {{
                        const filtered = participants.map(p => {{
                            return {{
                                id: p.id && p.id._serialized ? p.id._serialized : p.id
                            }};
                        }});
                        callback(JSON.stringify(filtered));
                    }})
                    .catch(err => callback({{ error: err.toString() }}));
            ";

            try
            {
                object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteAsyncScript(jsCode);

                if (result is IDictionary<string, object> err && err.ContainsKey("error"))
                {
                    response = "JavaScript error: " + err["error"];
                    failReason = FailReason.JavascriptError;
                    return false;
                }

                response = result?.ToString() ?? "[]";

                //Debug.WriteLine("Participants JSON:");
                //Debug.WriteLine(response);

                return true;
            }
            catch (OpenQA.Selenium.JavaScriptException jsEx)
            {
                response = "JavaScript error: " + jsEx.Message;
                Debug.WriteLine(response);
                failReason = FailReason.JavascriptError;
                return false;
            }
            catch (Exception ex)
            {
                response = "General error: " + ex.Message;
                failReason = FailReason.UnknownError;
                return false;
            }
        } // end of method

        public bool GetPastParticipants(string groupJid, out string response, out FailReason? failReason)
        {

            failReason = null;

            bool isWPPInjected = WPPConnectSafeInject(out _, out failReason);
            if (!isWPPInjected)
            {
                response = "GetPastParticipants FAILED because WPP Script can't be injected.";
                return false;
            }

            WhatsAppEvents?.Invoke(new WhatsAppEventArgs(EventState.StartGetPastParticipants));

            Debug.WriteLine($@"WhatsApp ==================== GROUP JID: {groupJid}");

            string jsCode = $@"
                const callback = arguments[arguments.length - 1];
                WPP.group.getPastParticipants('{groupJid}')
                    .then(participants => {{
                        const filtered = participants.map(p => {{
                            return {{
                                id: p.id && p.id._serialized ? p.id._serialized : p.id
                            }};
                        }});
                        callback(JSON.stringify(filtered));
                    }})
                    .catch(err => callback({{ error: err.toString() }}));
            ";

            try
            {
                object? result = ((IJavaScriptExecutor)Chrome.Driver!).ExecuteAsyncScript(jsCode);

                if (result is IDictionary<string, object> err && err.ContainsKey("error"))
                {
                    response = "JavaScript error: " + err["error"];
                    failReason = FailReason.JavascriptError;
                    return false;
                }

                response = result?.ToString() ?? "[]";

                Debug.WriteLine("Past participants JSON:");
                Debug.WriteLine(response);

                return true;
            }
            catch (OpenQA.Selenium.JavaScriptException jsEx)
            {
                response = "JavaScript error: " + jsEx.Message;
                Debug.WriteLine(response);
                failReason = FailReason.JavascriptError;
                return false;
            }
            catch (Exception ex)
            {
                response = "General error: " + ex.Message;
                failReason = FailReason.UnknownError;
                return false;
            }

        } // end of method

        public void InjectNameToWhatsApp(string name)
        {

            IJavaScriptExecutor js = (IJavaScriptExecutor)Chrome.Driver!;

            string escapedName = name.Replace("'", "\\'").Replace("\"", "\\\"");

            string nameScript = $@"
                var injectedId = 'blutik-wa-name';
                var existingInjected = document.getElementById(injectedId);
                if (existingInjected) {{
                    return; // sudah diinject, tidak perlu inject ulang
                }}

                var target = document.querySelector('span[aria-label=""WhatsApp""]');
                var fontSize = '24px';
                var marginBottom = '5px';

                if (!target) {{
                    // Coba cari <h1> dengan teks 'Chat'
                    var headings = document.querySelectorAll('h1');
                    for (var i = 0; i < headings.length; i++) {{
                        if (headings[i].textContent.trim() === 'Chat') {{
                            target = headings[i];
                            fontSize = '22px';
                            marginBottom = '0px';
                            break;
                        }}
                    }}
                }}

                if (target) {{
                    target.style.display = 'inline-flex';
                    target.style.alignItems = 'center';

                    var nameSpan = document.createElement('span');
                    nameSpan.textContent = ' {escapedName}';
                    nameSpan.id = injectedId; // penanda unik
                    nameSpan.style.marginLeft = '6px';
                    nameSpan.style.display = 'inline';
                    nameSpan.style.fontSize = fontSize;
                    nameSpan.style.marginBottom = marginBottom;
                    nameSpan.style.fontWeight = '100';

                    target.appendChild(nameSpan);
                }}
            ";

            js.ExecuteScript(nameScript);

        } // end of method

        public static string FilterPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return "";

            // Hanya menyisakan angka
            phone = Regex.Replace(phone, "[^0-9]", "");

            // Menghilangkan awalan "62" atau "0" (sekali saja, di awal string)
            phone = Regex.Replace(phone, @"^(62|0+)", "");

            // Tambahkan "0" di depan kembali
            phone = "62" + phone;

            return phone;
        } // end of method

    } // end of class


} // end of namespace
