using Magic.BrowserAutomationNET;
using Magic.SystemAddonsNET;
using OpenQA.Selenium;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Magic.SocialMediaNET
{
    // Magic versi 7.0.0

    public class Facebook
    {
        public class Account
        {
            public int Status { get; set; } = 0; // 0. not logged in; 1. logged in; -1. checkpoint; -2. sudah login, kelogout sendiri oleh fb
            public long Id { get; set; } = 0;
            public string Cookies { get; set; } = "";
            public bool IndonesiaLanguage { get; set; } = false;
        } // end of class

        public Chrome? Chrome { get; set; }
        public string? pageURL { get; set; }
        public string? facebookName { get; set; }
        public int timeout { get; set; } = 10;

        public Account CheckLoggedIn(string? cookies = null)
        {

            Account result = new Account();

            WebPage currentUrl;
            BrowserAutomationNET.WebElement userIDScript;
            string userIDJSON;
            string splits;
            string userID;
            bool stillLoggedIn = false;

            int maxIteration = cookies == null ? 1 : 3;

            // diulang 3x karena suka logout sendiri oleh FB ketika sudah login
            for (int i = 0; i < maxIteration; i++)
            {
                Chrome!.Navigate("https://www.facebook.com/profile.php");

                if (cookies != null)
                {
                    Chrome.AddCookiesFromJson(cookies);
                    Chrome.Refresh();
                }

                currentUrl = Chrome.GetCurrentUrl();

                if (currentUrl.Url.Contains("checkpoint"))
                {
                    result.Status = -1;
                    return result;
                }

                if (currentUrl.Url.Contains("m.facebook.com"))
                {
                    return CheckLoggedInMobile();
                }

                // ambil salah satu dari 3 tagname. 2 buah script untuk indikator sudah login. 1 buah input untuk indikator belum login.
                userIDScript = Chrome.FindElementByXPath("//script[@id='__eqmc']|//script[contains(text(), '__user')]|(//input[@data-testid='royal_pass'])[1]");

                if (userIDScript == null || !userIDScript.State || userIDScript.Item!.TagName == "input") continue;

                userIDJSON = userIDScript.Item.GetAttribute("innerHTML");
                splits = userIDJSON.Split(new string[] { "__user=" }, StringSplitOptions.None).ToList()[1];
                userID = splits.Split('&').ToList()[0]; // user id longint OR 0 if not logged in

                result.Id = Convert.ToInt64(userID);

                // ini terlogout sendiri oleh fb nya
                if (result.Id == 0) continue;

                (stillLoggedIn, result.IndonesiaLanguage) = CheckLanguage();

                if (stillLoggedIn)
                {
                    result.Status = 1;
                    result.Cookies = Chrome.GetAllCookiesToJson()!;

                    return result;
                }

                // ulangi sampai i kali jka gagal log in
            }

            return result;

        } // end of method

        public Account CheckLoggedInMobile()
        {
            Account result = new Account();

            // ambil salah satu dari 3 tagname. 2 buah script untuk indikator sudah login. 1 buah input untuk indikator belum login.
            BrowserAutomationNET.WebElement userIDaTag = Chrome!.FindElementByXPath("//div[@id='header']//a");

            if (userIDaTag == null) return result;

            string hrefValue = userIDaTag.Item!.GetAttribute("href");

            if (hrefValue.Contains("/login"))
            {
                return result;
            }

            string splits = hrefValue.Split(new string[] { "lst=" }, StringSplitOptions.None).ToList()[1];
            string userID = splits.Split('%').ToList()[0]; // user id longint OR 0 if not logged in

            // return user id if logged in. 0 otherwise.
            result.Id = Convert.ToInt64(userID);
            result.Status = 1;
            result.Cookies = Chrome.GetAllCookiesToJson()!;
            result.IndonesiaLanguage = CheckLanguageMobile();

            return result;
        } // end of method

        public void ChangeLanguage(string language)
        {
            language = language.ToLower();

            // check first
            bool isLanguageCorrect = true;
            (_, isLanguageCorrect) = CheckLanguage(language);

            if (isLanguageCorrect) return;

            // FB bahasa enggreeess, ubah ke bahasa endonesah
            Chrome!.Navigate("https://web.facebook.com/settings?tab=language&section=account&view");

            // EDIT BUTTON

            //BrowserAutomationNET.WebElement editButton = chrome.FindElementByXPath("(//div[@role='button']//span/span)[1]");
            //BrowserAutomationNET.WebElement editButtonORdialogWindow = Chrome.FindElementByXPath("//div[@role='button'][.//span[text()='OK']]|(//div[@role='main']//div[@role='button']//span/span)[1]");
            BrowserAutomationNET.WebElements editButtonANDdialogWindow = Chrome.FindElementsByXPath("//div[@role='button'][.//span[text()='OK']]|(//div[@role='main']//div[@role='button']//span/span)[1]");

            SafeClickResult safeClickResult;

            if (editButtonANDdialogWindow.Items.Count < 1)
            {
                return;
            }
            else if(editButtonANDdialogWindow.Items.Count > 1)
            {
                BrowserAutomationNET.WebElement dialogWindowOKButton = Chrome.FindElementByXPath("//div[@role='button'][.//span[text()='OK']]");
                safeClickResult = dialogWindowOKButton.SafeClick();
            }

            BrowserAutomationNET.WebElement editButton = Chrome.FindElementByXPath("(//div[@role='main']//div[@role='button']//span/span)[1]");
            safeClickResult = editButton.SafeClick();

            if (!safeClickResult.Status)
            {
                return;
            }

            // SELECT LANGUAGE

            BrowserAutomationNET.WebElement languageSelection = Chrome.FindElementByXPath("//div[@aria-haspopup='listbox']");

            if (!languageSelection.State)
            {
                return;
            }

            safeClickResult = languageSelection.SafeClick();

            if (!safeClickResult.Status)
            {
                return;
            }

            // PICK LANGUAGE

            BrowserAutomationNET.WebElement bahasaIndonesiaSelection = Chrome.FindElementByXPath("//span[text()='Bahasa Indonesia']");


            if (!bahasaIndonesiaSelection.State)
            {
                return;
            }

            safeClickResult = bahasaIndonesiaSelection.SafeClick();

            if (!safeClickResult.Status)
            {
                return;
            }

            // SAVE CHANGES

            Chrome.SendKeys(Keys.Tab);
            Chrome.SendKeys(Keys.Tab);
            Chrome.SendKeys(Keys.Space);

            Thread.Sleep(5000);

        } // end of method

        //
        // tuple pertama itu akun masih dalam keadaan terlogin. Karena bisa saja terlogout oleh FB nya
        // tuple kedua itu status checklanguagenya, misalnya : indonesia kah? true
        //
        private (bool, bool) CheckLanguage(string language = "indonesia")
        {
            language = language.ToLower();

            BrowserAutomationNET.WebElement checkedElement = Chrome!.FindElementByXPath("//*[@aria-label='Cari di Facebook']|//*[@aria-label='Search Facebook']");

            if(!checkedElement.State)
            {
                return (false, false);
            }

            string ariaLabel = checkedElement.Item!.GetAttribute("aria-label");

            if (language == "indonesia" && ariaLabel == "Cari di Facebook")
            {
                return (true, true);
            }
            else if (language == "english" && ariaLabel == "Search Facebook")
            {
                return (true, true);
            }

            return (true, false);

        } // end of method

        private bool CheckLanguageMobile(string language = "indonesia")
        {
            language = language.ToLower();

            BrowserAutomationNET.WebElement checkedElement = Chrome!.FindElementByXPath("//div[@id='profile_intro_card']//span[contains(text(), 'Edit')]");

            string ariaLabel = checkedElement.Item!.GetAttribute("innerHTML");

            if (language == "indonesia" && ariaLabel == "Edit info publik")
            {
                return true;
            }
            else if (language == "english" && ariaLabel == "Edit public details")
            {
                return true;
            }

            return false;

        } // end of method

        public bool SkipFreeData()
        {
            BrowserAutomationNET.WebElement tombolTidak = Chrome!.FindElementByXPath("//a/span[text()='Tidak, Terima Kasih']/..");

            if (!tombolTidak.State)
            {
                return false;
            }

            SafeClickResult safeClickResult = tombolTidak.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            BrowserAutomationNET.WebElement tombolOke = Chrome.FindElementByXPath("//button[@value='Oke, Gunakan Data']/span/..");

            if (!tombolOke.State)
            {
                return false;
            }

            safeClickResult = tombolOke.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            BrowserAutomationNET.WebElement loaded = Chrome.FindElementByXPath("//div[@id='MChromeHeader']");
            Thread.Sleep(2000);

            return true;
        } // end of method

        public string? PostURL { get; set; }
        public string? Cookies { get; set; }
        public string? Comment { get; set; }
        public string? Share { get; set; } = string.Empty;

        public bool SavePost(bool cookiesLoginFirst = false, int watchVideoDuration = 0)
        {
            if (cookiesLoginFirst)
            {
                Chrome!.Navigate("https://web.facebook.com");
                Chrome.AddCookiesFromJson(Cookies!);
            }

            Chrome!.Navigate(PostURL!);

            // TENTUKAN APAKAH VIDEO ATAU GAMBAR

            bool watchVideoChecked = WatchVideoCheck(watchVideoDuration);

            if (!watchVideoChecked) return false;

            Magic.BrowserAutomationNET.WebElement tindakanButton = Chrome.FindElementByXPath("//div[@aria-label='Tindakan untuk postingan ini' and @role='button']|//div[@aria-label='Menu' and @role='button' and .//i]");

            if(!tindakanButton.State)
            {
                return false;
            }

            string ariaLabelTindakanButton = tindakanButton.Item!.GetAttribute("aria-label");

            if(ariaLabelTindakanButton == "Tindakan untuk postingan ini")
            {
                return this.SavePost_Post(tindakanButton);
            }
            else
            {
                return this.SavePost_Reel(tindakanButton);
            }

        } // end of method

        private bool SavePost_Post(Magic.BrowserAutomationNET.WebElement tindakanButton)
        {

            SafeClickResult safeClickResult = tindakanButton.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            Magic.BrowserAutomationNET.WebElement simpanPostinganORVideoMenuItemSpan = Chrome!.FindElementByXPath("//div[@role='menuitem']//span[text()='Simpan postingan']|//div[@role='menuitem']//span[text()='Batal simpan kiriman']|//div[@role='menuitem']//span[text()='Simpan Video']|//div[@role='menuitem']//span[text()='Batal simpan video']");

            if(!simpanPostinganORVideoMenuItemSpan.State)
            {
                return false;
            }

            string spanText = simpanPostinganORVideoMenuItemSpan.Item!.Text;

            if (spanText == "Batal simpan kiriman" || spanText == "Batal simpan video")
            {
                safeClickResult = tindakanButton.SafeClick();

                if (!safeClickResult.Status)
                {
                    return false;
                }

                return true;
            }

            if (spanText == "Simpan postingan")
            {
                // GAMBAR

                Magic.BrowserAutomationNET.WebElement simpanPostinganMenuItem = Chrome!.FindElementByXPath("//div[@role='menuitem' and .//span[text()='Simpan postingan']]");

                safeClickResult = simpanPostinganMenuItem.SafeClick();

                if(!safeClickResult.Status)
                {
                    return false;
                }

            }
            else
            {
                // VIDEO

                Magic.BrowserAutomationNET.WebElement simpanPostinganMenuItem = Chrome!.FindElementByXPath("//div[@role='menuitem' and .//span[text()='Simpan Video']]");

                safeClickResult = simpanPostinganMenuItem.SafeClick();

                if (!safeClickResult.Status)
                {
                    return false;
                }
            }

            // SISANYA SAMA UNTUK GAMBAR DAN VIDEO

            Magic.BrowserAutomationNET.WebElement simpanKeSelesaiButton = Chrome.FindElementByXPath("//div[@aria-label='Selesai' and .//span[text()='Selesai']][ancestor::div[@aria-label='Simpan Ke' and @role='dialog']]");

            if (!simpanKeSelesaiButton.State)
            {
                return false;
            }

            safeClickResult = simpanKeSelesaiButton.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            Magic.BrowserAutomationNET.WebElement disimpanKeNotif = Chrome.FindElementByXPath("//*[contains(text(), 'Disimpan ke')]");

            return true;

        } // end of method

        private bool SavePost_Reel(Magic.BrowserAutomationNET.WebElement tindakanButton)
        {
            SafeClickResult safeClickResult = tindakanButton.SafeClick();

            if(!safeClickResult.Status)
            {
                return false;
            }

            Magic.BrowserAutomationNET.WebElement simpanReelMenuItemSpan = Chrome!.FindElementByXPath("//div[@role='menuitem']//span[text()='Simpan Reel']|//div[@role='menuitem']//span[text()='Batal Simpan Reel']");

            if(!simpanReelMenuItemSpan.State)
            {
                return false;
            }

            string spanText = simpanReelMenuItemSpan.Item!.Text;

            if(spanText == "Batal Simpan Reel")
            {
                safeClickResult = tindakanButton.SafeClick();
                return true;
            }

            Magic.BrowserAutomationNET.WebElement simpanReelMenuItem = Chrome.FindElementByXPath("//div[@role='menuitem' and .//span[text()='Simpan Reel']]");

            safeClickResult = simpanReelMenuItem.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            Magic.BrowserAutomationNET.WebElement disimpanKeNotif = Chrome.FindElementByXPath("//*[contains(text(), 'Disimpan ke')]");

            return true;
        } // end of method

        public bool LikePost(bool cookiesLoginFirst = false, int watchVideoDuration = 0)
        {

            if (cookiesLoginFirst)
            {
                Chrome!.Navigate("https://web.facebook.com");
                Chrome.AddCookiesFromJson(Cookies!);
            }

            Chrome!.Navigate(PostURL!);

            // TENTUKAN APAKAH VIDEO ATAU GAMBAR

            bool watchVideoChecked = WatchVideoCheck(watchVideoDuration);

            if (!watchVideoChecked) return false;

            Magic.BrowserAutomationNET.WebElement likeButton = Chrome.FindElementByXPath("//div[@aria-label='Suka' and @role='button' and .//div[@role='none']]|//div[@aria-label='Hapus Suka' and @role='button' and .//div[@role='none']]|//div[@aria-label='Suka Aktif' and @role='button']");

            string ariaLabelLikeButton = likeButton.Item!.GetAttribute("aria-label");

            if(ariaLabelLikeButton == "Hapus Suka" || ariaLabelLikeButton == "Suka Aktif")
            {
                return true;
            }

            SafeClickResult safeClickResult = likeButton.SafeClick();

            Magic.BrowserAutomationNET.WebElement activeLikeButton = Chrome.FindElementByXPath("//div[@aria-label='Hapus Suka' and @role='button' and .//div[@role='none']]|//div[@aria-label='Suka Aktif' and @role='button']");

            return true;
        } // end of method

        public bool SharePost(bool cookiesLoginFirst = false, int watchVideoDuration = 0)
        {
            if(cookiesLoginFirst)
            {
                Chrome!.Navigate("https://web.facebook.com");
                Chrome.AddCookiesFromJson(Cookies!);
            }

            Chrome!.Navigate(PostURL!);

            // TENTUKAN APAKAH VIDEO ATAU GAMBAR

            bool watchVideoChecked = WatchVideoCheck(watchVideoDuration);

            if (!watchVideoChecked) return false;

            // KLIK TOMBOL "BAGIKAN" (REELS) ATAU "KIRIM INI KE TEMAN" (STATUS)

            Magic.BrowserAutomationNET.WebElement shareButton = Chrome.FindElementByXPath("//div[@aria-label='Bagikan']|//div[contains(@aria-label, 'Kirim ini ke teman')]");

            if(!shareButton.State)
            {
                return false;
            }

            SafeClickResult safeClickResult = shareButton.SafeClick();

            // BISA LANGSUNG KLIK "BAGIKAN KE BERANDA" (GAMBAR) ATAU MESTI "OPSI LAINNYA" (VIDEO) DULU ATAU LANGSUNG DIALOG BAGIKAN (REELS)

            Magic.BrowserAutomationNET.WebElement opsiLainnyaORbagikanKeBerandaSpan = Chrome.FindElementByXPath("//div[@role='button']//span[contains(text(), 'Opsi Lainnya')]|//div[@role='button']//span[contains(text(), 'Bagikan ke Beranda')]|//div[@aria-label='Bagikan sekarang' and @role='button' and .//span[text()='Bagikan sekarang']]");

            string tagName = opsiLainnyaORbagikanKeBerandaSpan.Item!.TagName;

            if (tagName == "span")
            {
                string innerText = opsiLainnyaORbagikanKeBerandaSpan.Item!.Text;

                if (innerText == "Opsi Lainnya")
                {

                    Magic.BrowserAutomationNET.WebElement opsiLainnyaButton = Chrome.FindElementByXPath("//div[@role='button' and .//span[contains(text(), 'Opsi Lainnya')]]");
                    opsiLainnyaButton.SafeClick();

                }

                Magic.BrowserAutomationNET.WebElement bagikanKeBerandaButton = Chrome.FindElementByXPath("//div[@role='button' and .//span[contains(text(), 'Bagikan ke Beranda')]]");
                bagikanKeBerandaButton.SafeClick();
            }

            // TERBUKA DIALOG LANGSUNG "TULIS POSTINGAN" (SUDAH PERNAH BAGIKAN) ATAU "PEMIRSA DEFAULT DULU" (BARU PERTAMA KALI BAGIKAN) ATAU LANGSUNG "BAGIKAN" (REELS)

            Magic.BrowserAutomationNET.WebElement tulisPostinganDialog = Chrome.FindElementByXPath("//span[text()='Tulis Postingan']|//span[text()='Bagikan']");

            Magic.BrowserAutomationNET.WebElement pemirsaDefaultDialog = Chrome.FindElementByXPath("//span[text()='Pemirsa default']", 5);

            if(pemirsaDefaultDialog.State)
            {
                Magic.BrowserAutomationNET.WebElement radioPublik = Chrome.FindElementByXPath("//div[@role='radio' and .//span[contains(text(), 'Publik')]]");
                radioPublik.SafeClick();

                Magic.BrowserAutomationNET.WebElement selesaiButton = Chrome.FindElementByXPath("//div[@aria-label='Selesai' and @role='button' and .//span[contains(text(), 'Selesai')]]");
                selesaiButton.SafeClick();

                Thread.Sleep(3000);
            }

            // DIALOG "TULIS POSTINGAN" SUDAH READY. CEK DULU PRIVASINYA. GANTI DULU JADI PUBLIK BAGI YANG BELUM PUBLIK

            Magic.BrowserAutomationNET.WebElement editPrivasiButton = Chrome.FindElementByXPath("//div[contains(@aria-label, 'Edit privasi') and @role='button']");
            
            string ariaLabelEditPrivasiButton = editPrivasiButton.Item!.GetAttribute("aria-label");

            if (!ariaLabelEditPrivasiButton.Contains("Publik"))
            {
                editPrivasiButton.SafeClick();

                Magic.BrowserAutomationNET.WebElement radioPublik2 = Chrome.FindElementByXPath("//div[@role='radio' and .//span[contains(text(), 'Publik')]]");
                radioPublik2.SafeClick();

                Magic.BrowserAutomationNET.WebElement selesaiButton2 = Chrome.FindElementByXPath("//div[@aria-label='Selesai' and @role='button' and .//span[contains(text(), 'Selesai')]]|//div[@aria-label='Simpan' and @role='button' and .//span[contains(text(), 'Simpan')]]");
                selesaiButton2.SafeClick();

                Thread.Sleep(3000);
            }

            // INPUT SHARE TEXT

            if (Share != string.Empty)
            {
                Magic.BrowserAutomationNET.WebElement apaYangAndaPikirkanTextbox = Chrome.FindElementByXPath("//div[contains(@aria-label, 'Apa yang Anda pikirkan') and @role='textbox']|//div[contains(@aria-label, 'Katakan sesuatu tentang ini') and @role='textbox']");

                apaYangAndaPikirkanTextbox.SafeClick();

                if (!safeClickResult.Status)
                {
                    return false;
                }

                SafeSendKeysResult safeSendKeysResult = apaYangAndaPikirkanTextbox.SafeCopyAndPaste(Share!);

                if (!safeSendKeysResult.Status)
                {
                    return false;
                }
            }

            // KLIK TOMBOL SHARE

            Magic.BrowserAutomationNET.WebElement bagikanButton = Chrome.FindElementByXPath("//div[@aria-label='Bagikan' and @role='button' and .//span[text()='Bagikan']]|//div[@aria-label='Bagikan sekarang' and @role='button' and .//span[text()='Bagikan sekarang']]");
            bagikanButton.SafeClick();

            Magic.BrowserAutomationNET.WebElement sharedNotification = Chrome.FindElementByXPath("//*[text()='Dibagikan ke Beranda']|//*[text()='Dibagikan ke profil Anda']");

            // DONE

            return true;

        } // end of method

        /**
         * watchVideoDuration ini adalah prosentase 0 s/d 100
         */
        public bool CommentPost(bool cookiesLoginFirst = false, int watchVideoDuration = 0)
        {

            if (cookiesLoginFirst)
            {
                Chrome!.Navigate("https://web.facebook.com");
                Chrome.AddCookiesFromJson(Cookies!);
            }

            Chrome!.Navigate(PostURL!);

            // TENTUKAN APAKAH VIDEO ATAU GAMBAR

            bool watchVideoChecked = WatchVideoCheck(watchVideoDuration);

            if (!watchVideoChecked) return false;

            // APAKAH KOLOM INPUT KOMENTAR SUDAH MUNCUL (STATUS) ATAU HARUS KLIK TOMBOL KOMENTARI DULU (REELS)
            // ATAU TERKUNCI KARENA OTOMATIS

            Magic.BrowserAutomationNET.WebElement tulisKomentarInput = Chrome.FindElementByXPath("//div[@aria-label='Komentari']|//div[contains(@aria-label, 'Tulis komentar')]|//span[contains(text(), 'Kami mencurigai perilaku otomatis')]/ancestor::div//div[@aria-label='Tutup']");

            if (!tulisKomentarInput.State)
            {
                return false;
            }

            string ariaLabel = tulisKomentarInput.Item!.GetAttribute("aria-label");

            SafeClickResult safeClickResult;

            if(ariaLabel == "Tutup")
            {
                safeClickResult = tulisKomentarInput.SafeClick();
                tulisKomentarInput = Chrome.FindElementByXPath("//div[@aria-label='Komentari']|//div[contains(@aria-label, 'Tulis komentar')]");
                ariaLabel = tulisKomentarInput.Item!.GetAttribute("aria-label");
            }

            if (ariaLabel == "Komentari")
            {
                //tulisKomentarInput = Chrome.FindElementByXPath("//div[@aria-label='Komentari']//div");

                tulisKomentarInput.SafeClick();
                tulisKomentarInput = Chrome.FindElementByXPath("//div[contains(@aria-label, 'Tulis komentar')]");
            }

            // KOLOM INPUT TULIS KOMENTAR SUDAH ADA

            safeClickResult = tulisKomentarInput.SafeClick();

            if (!safeClickResult.Status)
            {
                return false;
            }

            SafeSendKeysResult safeSendKeysResult = tulisKomentarInput.SafeCopyAndPaste(Comment!);

            if (!safeSendKeysResult.Status)
            {
                return false;
            }

            safeSendKeysResult = tulisKomentarInput.SafeSendKeys(Keys.Enter);

            if(!safeSendKeysResult.Status)
            {
                return false;
            }

            Cookies = Chrome.GetAllCookiesToJson()!;

            return true;

        } // end of method

        public bool WatchVideoCheck(int watchVideoDuration = 0)
        {
            if (watchVideoDuration == 0) return true;

            Magic.BrowserAutomationNET.WebElement imageOrVideoAd = Chrome!.FindElementByXPath("//video|//img[number(@height) > 400]");

            if (!imageOrVideoAd.State)
            {
                return true;
            }

            string tagName = imageOrVideoAd.Item!.TagName;

            if (tagName == "video")
            {
                return WatchVideo(imageOrVideoAd, watchVideoDuration);
            }

            return true;
        } // end of method

        /**
         * watchVideoDuration ini adalah prosentase 0 s/d 100
         */
        public bool WatchVideo(Magic.BrowserAutomationNET.WebElement videoElement, int watchVideoDuration = 0)
        {
            if (videoElement == null || videoElement.Item == null)
            {
                Console.WriteLine("Video element is null or not found.");
                return true;
            }

            // Mengambil durasi video
            double videoDuration = Convert.ToDouble(videoElement.GetJavaScriptProperty("duration"));

            // Menghitung waktu yang akan ditonton berdasarkan persentase
            double watchTime = (watchVideoDuration / 100.0) * videoDuration * 0.95;

            // Mendapatkan waktu awal
            double currentTime = Convert.ToDouble(videoElement.GetJavaScriptProperty("currentTime"));
            double elapsedTime = currentTime;

            // Eksekusi JavaScript untuk memastikan browser memberikan keterangan bahwa ia sedang dalam fokus
            //((IJavaScriptExecutor)videoElement.Driver!).ExecuteScript("Object.defineProperty(document, 'hasFocus', { get: function() { return true; } });");

            // Memutar video hingga mencapai waktu yang ditentukan
            while (currentTime < watchTime)
            {
                // Menunggu sebentar sebelum memeriksa waktu saat ini lagi
                System.Threading.Thread.Sleep(1000);
                elapsedTime = elapsedTime + 1;

                // Mendapatkan waktu saat ini
                currentTime = Convert.ToDouble(videoElement.GetJavaScriptProperty("currentTime"));

                if ((elapsedTime - currentTime) >= timeout) return false;
            }

            Console.WriteLine($"Video watched for {watchVideoDuration}% of its duration.");

            //((IJavaScriptExecutor)videoElement.Driver!).ExecuteScript("delete document.hasFocus;");

            return true;
        } // end of method

        public int LikePage()
        {
            /***
             * Kode return :
             * 0. Tidak ditemukan tombol suka
             * 1. Berhasil klik suka
             * 2. Sudah disukai dari sebelumnya
             * 3. Gagal klik suka
             */
            Chrome!.Navigate(pageURL!);

            BrowserAutomationNET.WebElement likeButton = Chrome.FindElementByXPath("(//div[@aria-label='Suka'])[1]|//div[@aria-label='Disukai']");

            //div[@aria-label='Tindakan Lain']

            if (!likeButton.State)
            {
                return 0;
            }

            string ariaLabel = likeButton.Item!.GetAttribute("aria-label");

            if (ariaLabel == "Disukai")
            {
                return 2;
            }

            likeButton = Chrome.FindElementByXPath("(//div[@aria-label='Suka'])[1]/div");

            SafeClickResult safeClickResult = likeButton.SafeClick();

            if (!safeClickResult.Status)
            {
                return 3;
            }

            Thread.Sleep(5000);

            return 1;
        } // end of method

        public int ReviewPage(string reviewMessage)
        {
            /***
             * Kode return :
             * 0. Tidak ditemukan element yang diinginkan
             * 1. Berhasil membuat ulasan
             * 2. sudah pernah diulas sebelumnya menggunakan akun ini
             * 3. Gagal klik element yang diinginkan
             */

            Chrome!.Navigate(pageURL!);

            bool newPageModel = true;

            WebPage currentUrl = Chrome.GetCurrentUrl();

            if (currentUrl.Url.Contains("profile.php"))
            {
                Chrome.Navigate(currentUrl.Url + "&sk=reviews");
            }
            else
            {
                Chrome.Navigate(currentUrl.Url + "/reviews/?ref=page_internal");
                newPageModel = false;
            }

            /*
            BrowserAutomationNET.WebElement linkUlasan = chrome.FindElementByXPath("//span[text()='Ulasan']");

            if (!linkUlasan.State)
            {
                return 0;
            }

            SafeClickResult safeClickResult = linkUlasan.SafeClick();

            if (!safeClickResult.Status)
            {
                return 3;
            }
            */

            BrowserAutomationNET.WebElement tombolUlasanYa = Chrome.FindElementByXPath("//div[@aria-label='Ya' and @role='button']");

            if (!tombolUlasanYa.State) return 2;
            if (!tombolUlasanYa.SafeClick().Status) return 3;

            BrowserAutomationNET.WebElement spanTombolBerbagiKe = Chrome.FindElementByXPath("(//div[contains(@aria-label, 'Edit privasi')]//span)[last()]");

            if (!spanTombolBerbagiKe.State)
            {
                return 0;
            }

            string targetBerbagi = spanTombolBerbagiKe.Item!.Text;

            if (targetBerbagi != "Publik")
            {
                BrowserAutomationNET.WebElement tombolBerbagiKe = Chrome.FindElementByXPath("(//div[contains(@aria-label, 'Edit privasi')]//span)[last()]/../../..");

                if (!tombolBerbagiKe.State) return 0;
                if (!tombolBerbagiKe.SafeClick().Status) return 3;

                BrowserAutomationNET.WebElement tombolPublik = Chrome.FindElementByXPath("//span[text()='Pilih pemirsa']/../../../..//span[text()='Publik']/../../../../../../..");

                if (!tombolPublik.State) return 0;
                if (!tombolPublik.SafeClick().Status) return 3;

                BrowserAutomationNET.WebElement tombolSimpan = Chrome.FindElementByXPath("//div[@aria-label='Simpan']|//div[@aria-label='Selesai']");

                if (!tombolSimpan.State) return 0;
                if (!tombolSimpan.SafeClick().Status) return 3;

                for (int i = 0; i < 10; i++)
                {
                    spanTombolBerbagiKe = Chrome.FindElementByXPath("(//div[contains(@aria-label, 'Edit privasi')]//span)[last()]");

                    if (spanTombolBerbagiKe.Item!.Text != "Publik")
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (spanTombolBerbagiKe.Item.Text != "Publik")
                {
                    return 0;
                }
            } // end if not Public

            BrowserAutomationNET.WebElement kotakMessage = Chrome.FindElementByXPath("//div[contains(@aria-label, 'Apa yang Anda sarankan')]");

            if (!kotakMessage.State) return 0;
            if (!kotakMessage.SafeClick().Status) return 3;

            Magic.HelperNET.PutContentToClipboard(reviewMessage);
            Console.WriteLine(reviewMessage);

            Chrome.SendKeys("v", Keys.Control);

            BrowserAutomationNET.WebElement tombolPosting = Chrome.FindElementByXPath("//div[@aria-label='Posting']");

            if (!tombolPosting.State) return 0;
            if (!tombolPosting.SafeClick().Status) return 3;

            Thread.Sleep(10000);

            if (!newPageModel)
            {
                Chrome.Refresh();
            }

            BrowserAutomationNET.WebElement statusCreated = Chrome.FindElementByXPath($"(//div[text()='{reviewMessage}'])[1]");

            if (statusCreated.State) return 1;

            return 0;

        } // end of method

        public class Messenger
        {
            public Chrome? Chrome { get; set; }
            public int Timeout { get; set; } = 10;

            public bool BreakStatus { get; set; } = false;
            public bool StopFlag { get; set; } = false;
            public string? Message { get; set; }

            // EVENTS HERE

            #region DoneViewUnreadMessages Event
            public event EventHandler<DoneViewUnreadMessagesEventArgs>? DoneViewUnreadMessages;

            private void OnDoneViewUnreadMessages(DoneViewUnreadMessagesEventArgs e)
            {
                DoneViewUnreadMessages?.Invoke(this, e);
            } // end of method

            public class DoneViewUnreadMessagesEventArgs : EventArgs
            {
                public Chrome? Chrome { get; set; }
                public string? Message { get; set; }
            } // end of method
            #endregion

            #region MessengerLimit Event
            public event EventHandler<MessengerLimitEventArgs>? MessengerLimit;

            private void OnMessengerLimit(MessengerLimitEventArgs e)
            {
                MessengerLimit?.Invoke(this, e);
            } // end of method

            public class MessengerLimitEventArgs : EventArgs
            {
                public Chrome? Chrome { get; set; }
                public string? Message { get; set; }
            } // end of method
            #endregion

            #region StopFlag Event
            public event EventHandler<StopFlagEventArgs>? StopFlagEvent;

            private void OnStopFlag(StopFlagEventArgs e)
            {
                StopFlagEvent?.Invoke(this, e);
            } // end of method

            public class StopFlagEventArgs : EventArgs
            {
                public Chrome? Chrome { get; set; }
                public string? Message { get; set; }
            } // end of method
            #endregion

            #region PreviousMessagesLinkClick Event
            public event EventHandler<PreviousMessagesLinkClickEventArgs>? PreviousMessagesLinkClick;

            private void OnPreviousMessagesLinkClick(PreviousMessagesLinkClickEventArgs e)
            {
                PreviousMessagesLinkClick?.Invoke(this, e);
            } // end of method

            public class PreviousMessagesLinkClickEventArgs : EventArgs
            {
                public Chrome? Chrome { get; set; }
                public long LastMessageTime { get; set; }
                public long MessageAgeUnix { get; set; }
                public string? Message { get; set; }
            } // end of method
            #endregion

            // EVENTS THERE

            /// <summary>
            /// Method untuk membuka halaman messenger sampai message tertentu dengan usia message tertentu.
            /// </summary>
            /// <param name="messageAgeUnix">Usia message maksimal yang ingin ditampilkan dengan terus mengklik link "Lihat Pesan Sebelumnya..."</param>
            /// <param name="unread">Tampilkan hanya pesan yang belum dibuka (unread) atau seluruhnya?</param>
            /// <returns>Bool, berhasil atau tidaknya halaman message dibuka sampai pesan dengan usia tertentu</returns>
            public bool ViewMessages(long messageAgeUnix, bool unread = false)
            {
                DoneViewUnreadMessagesEventArgs doneViewUnreadMessagesEventArgs = new DoneViewUnreadMessagesEventArgs();
                doneViewUnreadMessagesEventArgs.Chrome = this.Chrome;
                doneViewUnreadMessagesEventArgs.Message = "Unread messages is viewed successfully.";

                string url = "https://mobile.facebook.com/messages/";
                string url2 = "https://m.facebook.com/messages/";

                if (unread)
                {
                    url = url + "?folder=unread";
                    url2 = url2 + "?folder=unread";
                }

                Chrome!.Navigate(url);

                string currentURL = Chrome.GetCurrentUrl().Url;

                for (int i = 0; i < 5; i++)
                {
                    if (currentURL == null)
                    {
                        Chrome.CloseBrowser();
                        Chrome.OpenBrowser();
                        Chrome.Navigate(url);
                        currentURL = Chrome.GetCurrentUrl().Url;
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentURL == null)
                {
                    return false;
                }

                Console.WriteLine(currentURL);

                // tidak bisa membuka mobile messenger
                if (!currentURL.Contains(url) && !currentURL.Contains(url2))
                {
                    #region execute MessengerLimit Event
                    MessengerLimitEventArgs messengerLimitEventArgs = new MessengerLimitEventArgs();
                    messengerLimitEventArgs.Chrome = Chrome;
                    messengerLimitEventArgs.Message = "Tidak bisa membuka mobile messenger.";
                    OnMessengerLimit(messengerLimitEventArgs);
                    #endregion

                    Magic.SocialMediaNET.Facebook facebook = new Magic.SocialMediaNET.Facebook();
                    facebook.Chrome = Chrome;

                    bool freeDataSkipped = facebook.SkipFreeData();

                    if (!freeDataSkipped)
                    {
                        return false;
                    }

                    Chrome.Navigate(url);
                }

                // loop nyoba-nyobain message yang mungkin yg dibutuhkan
                while (true)
                {
                    if (StopFlag)
                    {
                        BreakStatus = true;
                        Message = "Stop Flag is true from Facebook.Messenger class.";

                        #region execute StopFlag Event
                        StopFlagEventArgs stopFlagEventArgs = new StopFlagEventArgs();
                        stopFlagEventArgs.Chrome = this.Chrome;
                        stopFlagEventArgs.Message = Message;

                        OnStopFlag(stopFlagEventArgs);
                        #endregion

                        return false;
                    }

                    // 'threadlist_row_' ini memilih semua jenis message, baik itu perorangan ataupun non-perorangan
                    BrowserAutomationNET.WebElement lastMessageDate = Chrome.FindElementByXPath("(//div[@id='threadlist_rows']//div[contains(@id, 'threadlist_row_')])[last()]//abbr", Timeout);


                    if (!lastMessageDate.State)
                    {

                        BrowserAutomationNET.WebElement halamanWentWrong;

                        for (int i = 0; i < 5; i++)
                        {
                            halamanWentWrong = Chrome.FindElementByXPath("//div[contains(., 'Sorry, something went wrong.')]", 2);

                            if (halamanWentWrong.State)
                            {
                                Chrome.Refresh();

                                lastMessageDate = Chrome.FindElementByXPath("(//div[@id='threadlist_rows']//div[contains(@id, 'threadlist_row_')])[last()]//abbr", Timeout);

                                if (!lastMessageDate.State)
                                {
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

                    }

                    if (!lastMessageDate.State)
                    {
                        // tidak ada message
                        OnDoneViewUnreadMessages(doneViewUnreadMessagesEventArgs);
                        return true;
                    }


                    string messageDateDataString = lastMessageDate.Item!.GetAttribute("data-store");

                    MessageTimeDataStore messageTimeDataStore = JsonConvert.DeserializeObject<MessageTimeDataStore>(messageDateDataString)!;

                    long nowUnix = Magic.SystemAddonsNET.DateTime.NowUnix();

                    /*
                    System.Console.WriteLine("Time dari FB : " + (nowUnix - messageTimeDataStore.Time));
                    System.Console.WriteLine("From date dari setting : " + messageAgeUnix);
                    */

                    if ((nowUnix - messageAgeUnix) > messageTimeDataStore!.Time)
                    {
                        OnDoneViewUnreadMessages(doneViewUnreadMessagesEventArgs);
                        return true;
                    }

                    BrowserAutomationNET.WebElement linkLihatPesanSebelumnya = Chrome.FindElementByXPath("//div//*[contains(text(), 'Lihat Pesan Sebelumnya')]", Timeout);

                    if (linkLihatPesanSebelumnya.State)
                    {
                        #region execute PreviousMessagesLinkClick Event
                        PreviousMessagesLinkClickEventArgs previousMessagesLinkClickEventArgs = new PreviousMessagesLinkClickEventArgs();
                        previousMessagesLinkClickEventArgs.Chrome = this.Chrome;
                        previousMessagesLinkClickEventArgs.LastMessageTime = nowUnix - messageTimeDataStore.Time;
                        previousMessagesLinkClickEventArgs.MessageAgeUnix = messageAgeUnix;
                        previousMessagesLinkClickEventArgs.Message = "Proses klik link : Lihat Pesan Sebelumnya.";

                        OnPreviousMessagesLinkClick(previousMessagesLinkClickEventArgs);
                        #endregion

                        linkLihatPesanSebelumnya.SafeClick();

                        Thread.Sleep(2000);
                        Chrome.ScrollToBottom(2000);

                        continue;
                    }

                    OnDoneViewUnreadMessages(doneViewUnreadMessagesEventArgs);
                    return true;
                } // end of while
            } // end of method

            /// <summary>
            /// Mengambil semua message yang berasal dari marketplace di halaman yang terbuka. 
            /// Halaman yg terbuka merupakan halaman messenger yang sebelumnya sudah diproses oleh method <c>ViewMessages</c>.
            /// Data yang diambil dimasukkan ke dalam List of <c>MarketplaceMessage</c>.
            /// 
            /// Method ini tidak akan mengklik link "Lihat Pesan Sebelumnya..."
            /// Jadi hanya mengambil data yang muncul di screen.
            /// 
            /// Data yang diambil juga dibatasi hanya yang usia messagenya sesuai input parameter.
            /// </summary>
            /// <param name="messageAgeUnix">Usia pesan paling lama yang akan diambil</param>
            /// <returns>List of <c>MarketplaceMessage</c></returns>
            public List<MarketplaceMessage> GetMarketplaceMessages(long messageAgeUnix)
            {
                List<MarketplaceMessage> results = new List<MarketplaceMessage>();

                long nowUnix = Magic.SystemAddonsNET.DateTime.NowUnix();
                long sinceUnix = nowUnix - messageAgeUnix;

                // 'threadlist_row_thread_fbid' ini memilih message dengan jenis non-perorangan
                WebElements messages = Chrome!.FindElementsByXPath("//div[@id='threadlist_rows']//div[contains(@id, 'threadlist_row_thread_fbid')]");

                long id = 0;
                string url = "";
                bool read = false;
                long messageCreatedTime = 0;

                foreach (BrowserAutomationNET.WebElement webElement in messages.Items)
                {

                    BrowserAutomationNET.WebElement abbr = new BrowserAutomationNET.WebElement();

                    for (int i = 0; i < 5; i++)
                    {
                        abbr = webElement.FindElementByXPath(".//abbr");

                        if (abbr == null)
                        {
                            Chrome.Refresh();
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (abbr == null)
                    {
                        return null!;
                    }

                    string messageDateDataString = abbr.Item!.GetAttribute("data-store");

                    MessageTimeDataStore messageTimeDataStore = JsonConvert.DeserializeObject<MessageTimeDataStore>(messageDateDataString)!;

                    messageCreatedTime = messageTimeDataStore!.Time;

                    if (messageCreatedTime < sinceUnix)
                    {
                        continue;
                    }

                    BrowserAutomationNET.WebElement link = webElement.FindElementByXPath(".//a");
                    BrowserAutomationNET.WebElement h3 = webElement.FindElementByXPath(".//h3");

                    url = link.Item!.GetAttribute("href");

                    string splits = url.Split(new string[] { "cid.g." }, StringSplitOptions.None).ToList()[1];
                    id = Convert.ToInt64(splits.Split('&').ToList()[0]);

                    string fontWeight = h3.GetCssValue("font-weight")!;

                    // bisa terjadi h3 null, karena bot kecapekan
                    if (fontWeight != null && fontWeight == "400")
                    {
                        read = true;
                    }
                    else
                    {
                        read = false;
                    }

                    results.Add(new MarketplaceMessage(id, url, read, messageCreatedTime));
                }

                return results;
            } // end of method

            public class MarketplaceMessage
            {
                public long id { get; set; }
                public string? url { get; set; }
                public bool read { get; set; }
                public long time { get; set; }

                public MarketplaceMessage(long id, string url, bool read, long time)
                {
                    this.id = id;
                    this.url = url;
                    this.read = read;
                    this.time = time;
                } // end of method
            } // end of class

            /// <summary>
            /// Mengambil semua komponen dalam sebuah message marketplace.
            /// </summary>
            /// <param name="facebookUID">ID akun facebook yang sekarang sedang aktif</param>
            /// <param name="URL">URL sebuah chat. Jika tidak diisi maka digunakan halaman yang terbuka saat ini.</param>
            /// <returns>Object <c>MarketplaceMessageData</c></returns>
            public MarketplaceMessageData GetMarketplaceMessageData(long facebookUID = 0, string URL = "")
            {
                MarketplaceMessageData marketplaceMessageData = new MarketplaceMessageData();

                if (URL.Trim() != "")
                {
                    Chrome!.Navigate(URL);
                }

                if (StopFlag)
                {
                    BreakStatus = true;
                    Message = "Stop Flag is true getting marketplace message data.";

                    StopFlagEventArgs stopFlagEventArgs = new StopFlagEventArgs();
                    stopFlagEventArgs.Chrome = this.Chrome;
                    stopFlagEventArgs.Message = Message;

                    OnStopFlag(stopFlagEventArgs);

                    return null!;
                }

                // element ini untuk mengambil title product & menentukan apakah ini message marketplace
                BrowserAutomationNET.WebElement titleElement = Chrome!.FindElementByXPath("((//div[@id='root']//a)[1]/div/div)[last()]", Timeout);


                if (!titleElement.State)
                {

                    BrowserAutomationNET.WebElement halamanWentWrong;

                    for (int i = 0; i < 5; i++)
                    {
                        halamanWentWrong = Chrome.FindElementByXPath("//div[contains(., 'Sorry, something went wrong.')]", 2);

                        if (halamanWentWrong.State)
                        {
                            Chrome.Refresh();

                            titleElement = Chrome.FindElementByXPath("((//div[@id='root']//a)[1]/div/div)[last()]", Timeout);

                            if (!titleElement.State)
                            {
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

                }

                if (!titleElement.State)
                {
                    // tidak ada message
                    return null!;
                }


                // element ini untuk mengambil data nama yg terakhir chat
                BrowserAutomationNET.WebElement lastMessageBubble = Chrome.FindElementByXPath("(//div[@data-sigil='message-xhp marea'])[last()]", Timeout);

                // element ini untuk mengambil UID yg terakhir ngechat & timestamp
                BrowserAutomationNET.WebElement lastMessageWrapperElement = Chrome.FindElementByXPath("(//div[@data-sigil='message-text'])[last()]", Timeout);

                // element ini untuk mengambil isi messagenya
                BrowserAutomationNET.WebElement lastMessageElement = Chrome.FindElementByXPath("(//div[@data-sigil='message-text']//span//div)[last()]", Timeout);


                // element ini untuk mengambil URL product
                BrowserAutomationNET.WebElement urlLinkElement = Chrome.FindElementByXPath("//div[@id='root']//a", Timeout);

                // MESSAGE FROM
                string lastMessageBubbleDataStoreJson = lastMessageBubble.Item!.GetAttribute("data-store");
                MessageBubbleDataStore lastMessageBubbleDataStore = JsonConvert.DeserializeObject<MessageBubbleDataStore>(lastMessageBubbleDataStoreJson)!;
                marketplaceMessageData.MessageFrom = lastMessageBubbleDataStore.Name;

                // MESSAGE FROM UID & MESSAGE TIME
                string lastMessageWrapperElementDataStoreJson = lastMessageWrapperElement.Item!.GetAttribute("data-store")!;
                MessageWrapperElementDataStore lastMessageWrapperElementDataStore = JsonConvert.DeserializeObject<MessageWrapperElementDataStore>(lastMessageWrapperElementDataStoreJson)!;
                marketplaceMessageData.MessageFromUid = lastMessageWrapperElementDataStore.Author;
                marketplaceMessageData.MessageTime = lastMessageWrapperElementDataStore.Timestamp / 1000;

                // LAST MESSAGE
                marketplaceMessageData.LastMessage = lastMessageElement.Item!.GetAttribute("innerHTML");

                // PRODUCT TITLE
                string title = titleElement.Item!.Text;
                marketplaceMessageData.ProductTitle = title.Split(new[] { " (Kode" }, StringSplitOptions.None).ToList()[0];

                // PRODUCT URL
                string urlLink = urlLinkElement.Item!.GetAttribute("href");
                marketplaceMessageData.ProductURL = urlLink.Replace("mobile.facebook.com", "web.facebook.com");

                string urlPart2 = urlLink.Split(new string[] { "item/" }, StringSplitOptions.None)[1];
                marketplaceMessageData.ProductId = Convert.ToInt64(urlPart2.Split('/')[0]);

                if (facebookUID != 0)
                {
                    marketplaceMessageData.SelfUid = facebookUID;
                    marketplaceMessageData.LastSelfMessage = GetLastSelfMessage(facebookUID);
                }

                return marketplaceMessageData;
            } // end of method


            /// <summary>
            /// Mendapatkan last self message, apapun itu. Baik dari template message ataupun bukan.
            /// Diambil dari halaman single chatting.
            /// </summary>
            /// <param name="facebookUID">ID akun FB yang diinginkan</param>
            /// <param name="url">Halaman single chat. Jika tidak diisi, maka digunakan halaman yang terbuka saat ini.</param>
            /// <returns></returns>
            public string GetLastSelfMessage(long facebookUID, string url = "")
            {
                bool safe = false;

                if (url.Trim() != "")
                {
                    Chrome!.Navigate(url);
                }

                BrowserAutomationNET.WebElement lastSelfMessageElement = new BrowserAutomationNET.WebElement();

                while (true)
                {
                    // pastikan element sudah terloading
                    BrowserAutomationNET.WebElement tambahkanStikerButton = Chrome!.FindElementByXPath("//button[@aria-label='Tambahkan Stiker']", Timeout);

                    if (!tambahkanStikerButton.State)
                    {

                        BrowserAutomationNET.WebElement halamanWentWrong;

                        for (int i = 0; i < 5; i++)
                        {
                            halamanWentWrong = Chrome.FindElementByXPath("//div[contains(., 'Sorry, something went wrong.')]", 2);

                            if (halamanWentWrong.State)
                            {
                                Chrome.Refresh();

                                tambahkanStikerButton = Chrome.FindElementByXPath("//button[@aria-label='Tambahkan Stiker']", Timeout);

                                if (!tambahkanStikerButton.State)
                                {
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

                    }

                    if (!tambahkanStikerButton.State)
                    {
                        safe = false;
                        break;
                    }

                    lastSelfMessageElement = Chrome.FindElementByXPath($"(//div[@data-sigil='message-text' and contains(@data-store, '{facebookUID}')]//span//div)[last()]", 1);

                    if (lastSelfMessageElement.State)
                    {
                        safe = true;
                        break;
                    }

                    // klik tombol/link lihat pesan sebelumnya perlu dilakukan jika last self messagenya tersembunyi di atas, karena banyak message di bawahnya

                    BrowserAutomationNET.WebElement linkLihatPesanSebelumnya = Chrome.FindElementByXPath("//div//*[contains(text(), 'Lihat Pesan Sebelumnya')]", 1);

                    if (linkLihatPesanSebelumnya.State)
                    {
                        linkLihatPesanSebelumnya.SafeClick();
                        Thread.Sleep(3000);

                        continue;
                    }
                    else
                    {
                        safe = false;
                        break;
                    }
                }

                if (!safe) return null!;

                return lastSelfMessageElement.Item!.GetAttribute("innerHTML");
            } // end of method

            /// <summary>
            /// Melakukan balas message di halaman single chatting.
            /// </summary>
            /// <param name="message">Message yang mau disend.</param>
            /// <param name="url">URL halaman single chat. Jika tidak diisi, maka gunakan halaman yang terbuka saat ini.</param>
            /// <returns></returns>
            public bool ReplyMessage(string message, string url = "")
            {
                if (url.Trim() != "")
                {
                    Chrome!.Navigate(url);
                }

                BrowserAutomationNET.WebElement textAreaElement = Chrome!.FindElementByXPath("//textarea[@id='composerInput']", Timeout);

                Magic.HelperNET.PutContentToClipboard(message);

                SafeSendKeysResult safeSendKeysResult = textAreaElement.SafeSendKeys(Keys.Control + "v", Timeout);

                if (!safeSendKeysResult.Status)
                {
                    return false;
                }

                Thread.Sleep(2000);

                if (StopFlag)
                {
                    BreakStatus = true;
                    message = "Stop Flag is true when ready to click send message button.";

                    #region execute StopFlag Event
                    StopFlagEventArgs stopFlagEventArgs = new StopFlagEventArgs();
                    stopFlagEventArgs.Chrome = this.Chrome;
                    stopFlagEventArgs.Message = message;

                    OnStopFlag(stopFlagEventArgs);
                    #endregion

                    return false;
                }

                BrowserAutomationNET.WebElement kirimButton = Chrome.FindElementByXPath("//button[@value='Kirim']", Timeout);

                if (kirimButton.State)
                {
                    try
                    {
                        kirimButton.SafeClick();
                    }
                    catch
                    {
                        return false;
                    }
                }

                Thread.Sleep(5000);

                // move away from message page, supaya di loop berikutnya tidak langsung terbuka unread messagenya
                Chrome.Navigate("https://blank.org");
                Chrome.FindElementByXPath("//a[text()='.']", Timeout);

                return true;
            } // end of method

            /// <summary>
            /// Buat halaman single chat jadi unread.
            /// </summary>
            /// <param name="url">URL halaman single chat. Jika tidak diisi, maka digunakan halaman yang terbuka saat ini.</param>
            public void MakeUnread(string url = "")
            {
                if (url.Trim() != "")
                {
                    Chrome!.Navigate(url);
                }

                BrowserAutomationNET.WebElement selection = Chrome!.FindElementByXPath("//div[@id='root']//select");
                SelectElement selectElement = new SelectElement(selection.Item!);
                selectElement.SelectByText("Tandai sebagai belum dibaca");
            } // end of method

            public class MessageTimeDataStore
            {
                public long Time { get; set; }
            } // end of class

            public class MessageBubbleDataStore
            {
                public string? Name { get; set; }
                public bool Has_attachment { get; set; }
            } // end of class

            public class MessageWrapperElementDataStore
            {
                public long Timestamp { get; set; }
                public long Author { get; set; }
                public string? Uuid { get; set; }
            } // end of class

            public class MarketplaceMessageData
            {
                public string? MessageFrom { get; set; }
                public long MessageFromUid { get; set; }
                public string? LastMessage { get; set; }
                public long MessageTime { get; set; }
                public string? ProductTitle { get; set; }
                public string? ProductURL { get; set; }
                public long ProductId { get; set; }
                public long SelfUid { get; set; }
                public string? LastSelfMessage { get; set; }
            } // end of class
        } // end of class
    } // end of class
} // end of namespace
