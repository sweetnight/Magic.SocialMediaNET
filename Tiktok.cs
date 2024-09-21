using Magic.BrowserAutomationNET;
using System.Diagnostics;
using System.Security.Policy;

namespace Magic.SocialMediaNET
{
    public class Tiktok
    {

        public class EventType
        {

            public int Code { get; }
            public string Message { get; }

            private EventType(int code, string message)
            {

                Code = code;
                Message = message;
            }

            public static EventType AccountNotValid { get; } = new EventType(1, "Akun belum diinisasi atau belum login");
            public static EventType TapStarting { get; } = new EventType(2, "Start Tap");
            public static EventType CommentStarting { get; } = new EventType(3, "Start Comment atau Reply Comment");
            public static EventType LiveEnded { get; } = new EventType(4, "Live tidak ada, mungkin sudah selesai");
            public static EventType TapDone { get; } = new EventType(4, "Tap sudah selesai");
            public static EventType CommentDone { get; } = new EventType(5, "Comment sudah selesai");
            public static EventType ManualStopAtTapping { get; } = new EventType(5, "Manual stop ketika lagi tap");

        } // end of class

        private Action<TikTokEventArgs>? _tikTokEvent;

        public event Action<TikTokEventArgs>? TikTokEvent
        {
            add
            {
                // Gunakan private field untuk memeriksa dan menambahkan delegate
                if (_tikTokEvent == null || !_tikTokEvent.GetInvocationList().Contains(value))
                {
                    _tikTokEvent += value;
                }
            }
            remove
            {
                _tikTokEvent -= value;
            }
        }

        public class TikTokEventArgs
        {
            public EventType EventType { get; set; }
            public Chrome? Chrome { get; set; }
            public TikTokAccount? Account { get; set; }

            public TikTokEventArgs(EventType eventType, Chrome chrome, TikTokAccount? account = null)
            {

                this.EventType = eventType;
                this.Account = account;
                this.Chrome = chrome;

            } // end of constructor method

        } // end of class

        public Chrome? Chrome { get; set; }
        public TikTokAccount? Account { get; set; }
        int Timeout { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }

        public Tiktok(Chrome chrome, int timeout, CancellationTokenSource cancellationTokenSource)
        {

            this.Chrome = chrome;
            this.Timeout = timeout;
            this.CancellationTokenSource = cancellationTokenSource;

        } // end of method

        public void CheckLoggedIn()
        {

            TikTokAccount result = new TikTokAccount();

            Chrome!.Navigate("https://www.tiktok.com");

            // ambil salah satu dari 3 tagname. 2 buah script untuk indikator sudah login. 1 buah input untuk indikator belum login.
            BrowserAutomationNET.WebElement navProfile = Chrome!.FindElementByXPath("//a[@data-e2e='nav-profile' and starts-with(@href, '/@') and string-length(@href) > 2]|//button[@type='button' and @data-e2e='nav-login-button']", Timeout);

            /*
            <button type="button" data-e2e="nav-login-button" class="efna91q2 css-ns1v7o-Button-StyledLogin ehk74z00">Log in</button>
            */

            if (!navProfile.State || navProfile.Item!.TagName == "button")
            {
                result.Status = TikTokAccountStatus.NotLoggedIn;
                Account = result;

                return;
            }

            string hrefValue = navProfile.Item!.GetAttribute("href");

            // Cek apakah href exact "/@" atau tidak
            if (hrefValue != "/@")
            {
                // Ambil substring setelah '@'
                string username = hrefValue.Substring(hrefValue.IndexOf('@'));

                result.Status = TikTokAccountStatus.LoggedIn;
                result.Id = username;
                result.Cookies = Chrome.GetAllCookiesToJson()!;
            }

            Account = result;

        } // end of method

        public void Tap(int tapAmount, int delayFrom, int delayTo, string? liveLink = null)
        {

            if (this.Account == null || this.Account.Status != TikTokAccountStatus.LoggedIn)
            {
                _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.AccountNotValid, Chrome!));
            }

            if(liveLink != null)
            {
                Chrome!.Navigate(liveLink);
            }

            bool isLiveEnded = this.IsLiveEnded(Chrome!);

            if (isLiveEnded)
            {
                _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.LiveEnded, Chrome!));
                return;
            }

            _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.TapStarting, Chrome!));

            Debug.WriteLine($"=========== tap tap sebanyak {tapAmount} kali");

            WebElement likeContainer = Chrome!.FindElementByXPath($@"//div[contains({Chrome.ToLower("@class")}, 'divlikecontainer') and .//div[contains({Chrome.ToLower("@class")}, 'divlikebtnicon')]]", Timeout);

            Thread.Sleep(5000);

            int delay;
            SafeClickResult safeClickResult;

            for (int i = 0; i < tapAmount; i++)
            {
                //div[contains(@class, 'DivLikeContainer')]
                Debug.WriteLine($"tap ke-{i + 1} dari {tapAmount}");
                safeClickResult = likeContainer.SafeClick();

                if(!safeClickResult.Status)
                {
                    Thread.Sleep(Timeout);
                    i--;
                }

                if (CancellationTokenSource.IsCancellationRequested)
                {
                    _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.ManualStopAtTapping, Chrome));
                    return;
                }

                delay = HelperNET.RandomNumberBetween(delayFrom, delayTo);
                Debug.WriteLine($"delay selama {delay} ms");
                Thread.Sleep(delay);
            }

            _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.TapDone, Chrome!));

        } // end of method

        public void Comment(List<string> comments, int delayFrom, int delayTo, string? liveLink = null)
        {
            if (this.Account == null || this.Account.Status != TikTokAccountStatus.LoggedIn)
            {
                _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.AccountNotValid, Chrome!));
            }

            if (liveLink != null)
            {
                Chrome!.Navigate(liveLink);
            }

            bool isLiveEnded = this.IsLiveEnded(Chrome!);

            if (isLiveEnded)
            {
                _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.LiveEnded, Chrome!));
                return;
            }

            _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.CommentStarting, Chrome!));

            Debug.WriteLine("============ komen komen komen komen komen komen komen komen komen");

            WebElement commentInput = Chrome!.FindElementByXPath($@"//div[{Chrome.ToLower("@data-e2e")}='comment-input']//div[{Chrome.ToLower("@contenteditable")}='plaintext-only']", Timeout);

            foreach(string comment in comments)
            {
                Magic.HelperNET.PutContentToClipboard(comment);

                commentInput.SafeSendKeys(OpenQA.Selenium.Keys.Control + "v");
                commentInput.SafeSendKeys(OpenQA.Selenium.Keys.Enter);
            }

            _tikTokEvent?.Invoke(new TikTokEventArgs(EventType.CommentDone, Chrome!));

        } // end of method

        public bool IsLiveEnded(Chrome chrome)
        {

            WebElement playerContainer = Chrome!.FindElementByXPath($@"//div[contains({Chrome.ToLower("@class")}, 'divliveroomplayerwrapper')]");
            WebElement videoElement = Chrome.FindElementByXPath($@"//div[contains({Chrome.ToLower("@class")}, 'divliveroomplayerwrapper')]//video", 1);

            if (!videoElement.State)
            {
                return true;
            }
            else
            {
                return false;
            }

        } // end of method

        public string GetUsernameFromLiveLink(string liveLink)
        {

            string username = string.Empty;

            int startIndex = liveLink.IndexOf("/@"); // Mulai dari '/'
            int endIndex = liveLink.IndexOf('/', startIndex + 2); // Cari '/' setelah username

            if (startIndex >= 0 && endIndex > startIndex)
            {
                username = liveLink.Substring(startIndex, endIndex - startIndex);
            }

            return username;

        } // end of method

    } // end of class

    public class TikTokAccount
    {
        public TikTokAccountStatus Status { get; set; } = TikTokAccountStatus.NotLoggedIn;
        public string Id { get; set; } = string.Empty;
        public string Cookies { get; set; } = string.Empty;
    } // end of class

    public enum TikTokAccountStatus
    {

        NotLoggedIn = 0,
        LoggedIn = 1,
        Banned = -1

    } // end of enum

} // end of namespace
