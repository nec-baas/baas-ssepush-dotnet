using Nec.Nebula.Internal;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nec.Nebula.IT
{
    class ITUtil
    {
        // ユーザ情報
        public const string Username = "DotnetPushUser1";
        public const string Email = "DotnetPushUser1@example.com";
        public const string Password = "Passw0rD";

        // インスタレーションを保存する端末ストレージの場所
        public static string InstallationFilename = Application.LocalUserAppDataPath + "\\InstallationSettings.config";

        private static readonly DateTime EpochDate = new DateTime(1970, 1, 1);

        // Nebulaサービス初期化
        public static void InitNebula(NbService service = null, int no = 0)
        {
            try
            {
                service = service ?? NbService.Singleton;
            }
            catch (NullReferenceException)
            {
                // CommonITでNbServiceをDisposeしているので、
                // フェールセーフ処理を入れておく
                service = NbService.GetInstance();
            }

            service.EndpointUrl = TestConfig.NebulaEndpointUrl;
            service.DisableOffline();

            // for MultiTenant
            /*
            if (1 == no)
            {
                service.TenantId = TenantId2;
                service.AppId = AppId2;
                service.AppKey = AppKey2;
            }
            else
            {
             */
                service.TenantId = TestConfig.TenantId;
                service.AppId = TestConfig.AppId;
                service.AppKey = TestConfig.AppKey;
            //}
        }

        // Nebulaサービス初期化(クライアントPush無効アプリ)
        public static void InitNebulaForPushDisabled()
        {
            NbService service = null;

            service = NbService.Singleton;

            service.EndpointUrl = TestConfig.NebulaEndpointUrl;
            service.DisableOffline();

            service.TenantId = TestConfig.TenantId;
            service.AppId = TestConfig.AppIdForPushDisabled;
            service.AppKey = TestConfig.AppKeyForPushDisabled;
        }

        public static void UseNormalKey(NbService service = null, int no = 0)
        {
            service = service ?? NbService.Singleton;
            // for MultiTenant
            /*
            if (1 == no)
            {
                service.AppKey = AppKey2;
            }
            else
            {*/
            service.AppKey = TestConfig.AppKey;
            //}
        }

        public static void UseNormalKeyForPushDisabled(NbService service = null, int no = 0)
        {
            service = service ?? NbService.Singleton;

            service.AppKey = TestConfig.AppKeyForPushDisabled;
        }

        public static void UseMasterKey(NbService service = null, int no = 0)
        {
            service = service ?? NbService.Singleton;

            service.AppKey = TestConfig.MasterKey;
        }
        /*
        public static void UseNormalKeyForPushDisabled(NbService service = null)
        {
            service = service ?? NbService.Singleton;

            service.AppKey = AppKeyForPushDisabled;
        }
        */
        public static void UseMasterKeyForPushDisabled(NbService service = null)
        {
            service = service ?? NbService.Singleton;

            service.AppKey = TestConfig.MasterKeyForPushDisabled;
        }

        // for MultiApp
        public static void UseAppIDKey(int no = 0, bool isApp = true)
        {
            var service = NbService.Singleton;
            /*
            if (1 == no)
            {
                service.AppId = AppId3;
                if (isApp)
                    service.AppKey = AppKey3;
                else
                    service.AppKey = MasterKey3;
            }
            else
            {*/
                service.AppId = TestConfig.AppId;
                if (isApp)
                    service.AppKey = TestConfig.AppKey;
                else
                    service.AppKey = TestConfig.MasterKey;
            //}
        }

        /// <summary>
        /// サインアップ・ログインする。
        /// </summary>
        /// <returns>ログイン中ユーザ、ログインに失敗した場合は空のインスタンスが返る</returns>
        public static async Task<NbUser> SignUpAndLogin()
        {
            var user = new NbUser
            {
                Username = Username,
                Email = Email
            };

            return await SignUpAndLogin(user);
        }

        /// <summary>
        /// サインアップ・ログインする。
        /// </summary>
        /// <param name="user">ユーザ</param>
        /// <returns>ログイン中ユーザ、ログインに失敗した場合は空のインスタンスが返る</returns>
        public static async Task<NbUser> SignUpAndLogin(NbUser user)
        {
            NbUser loggedInUser = new NbUser();
            try
            {
                await user.SignUpAsync(Password);
                loggedInUser = await NbUser.LoginWithUsernameAsync(user.Username, Password);
            }
            catch (Exception)
            {
                // do nothing
            }

            return loggedInUser;
        }

        /// <summary>
        /// ログアウトする。
        /// </summary>
        /// <returns>成否</returns>
        public static async Task<bool> Logout()
        {
            bool result = true;

            if (NbUser.IsLoggedIn())
            {
                try
                {
                    await NbUser.LogoutAsync();

                }
                catch (Exception)
                {
                    result = false;
                }
            }

            return result;
        }

        /// <summary>
        /// グループを作成する。
        /// </summary>
        /// <returns>作成したグループ</returns>
        public static async Task<NbGroup> CreateGroup(ISet<string> users)
        {
            var acl = NbAcl.CreateAclForAnonymous();
            var group = new NbGroup("group");
            group.Acl = acl;
            if (users != null)
            {
                group.Users = users;
            }
            return await group.SaveAsync();
        }

        /// <summary>
        /// 全ユーザ削除
        /// </summary>
        public static void DeleteAllUsers()
        {
            ITUtil.UseMasterKey();

            var users = NbUser.QueryUserAsync().Result;
            foreach (var user in users)
            {
                user.DeleteAsync().Wait();
            }

            ITUtil.UseNormalKey();
        }

        /// <summary>
        /// 全グループ削除
        /// </summary>
        public static void DeleteAllGroups()
        {
            ITUtil.UseMasterKey();
            
            var groups = NbGroup.QueryGroupsAsync().Result;
            foreach (var group in groups)
            {
                group.DeleteAsync().Wait();
            }
            
            ITUtil.UseNormalKey();
        }

        /// <summary>
        /// 全アプリのインスタレーションを削除
        /// </summary>
        public static void DeleteInstallationsOfAllApp()
        {
            InitNebula();
            DeleteInstallationsForPushEnabled();

            InitNebulaForPushDisabled();
            DeleteInstallationsForPushDisabled();
        }

        /// <summary>
        /// 全インスタレーション削除(Push送信許可アプリ)
        /// </summary>
        public static void DeleteInstallationsForPushEnabled()
        {
            ITUtil.UseMasterKey();

            try
            {
                DeleteInstallations();
            }
            catch (Exception)
            {
                Assert.Fail("DeleteInstallations() Fail");
            }
            finally
            {
                ITUtil.UseNormalKey();
            }
        }

        /// <summary>
        /// 全インスタレーション削除(Push送信禁止アプリ)
        /// </summary>
        public static void DeleteInstallationsForPushDisabled()
        {
            ITUtil.UseMasterKeyForPushDisabled();

            try
            {
                DeleteInstallations();
            }
            catch (Exception)
            {
                Assert.Fail("DeleteInstallations() Fail");
            }
            finally
            {
                ITUtil.UseNormalKeyForPushDisabled();
            }
        }

        private static void DeleteInstallations()
        {
            var service = NbService.Singleton;
            NbRestRequest request;

            // リクエスト生成
            request = service.RestExecutor.CreateRequest("/push/installations", HttpMethod.Get);

            // リクエスト送信
            var installations = service.RestExecutor.ExecuteRequestForJson(request).Result;

            if (installations != null)
            {
                NbJsonArray jsonArray = installations.GetArray("results");
                foreach (NbJsonObject obj in jsonArray)
                {
                    string installationId = obj["_id"].ToString();
                    DeleteInstallation(installationId);
                }
            }
        }

        private static void DeleteInstallation(string InstallationId)
        {
            var service = NbService.Singleton;

            var req = service.RestExecutor.CreateRequest("/push/installations/{installationId}", HttpMethod.Delete)
                .SetUrlSegment("installationId", InstallationId);

                service.RestExecutor.ExecuteRequest(req);
        }

        // ストレージ内情報を削除
        public static void DeleteStorage()
        {
            System.IO.File.Delete(InstallationFilename);
        }

        /// <summary>
        /// 現在実行中のアセンブリ参照を取得してバージョンを取得する
        /// </summary>
        /// <returns>アセンブリバージョン</returns>
        public static string GetVersionName()
        {
            var asmbl = Assembly.GetEntryAssembly();
            if (asmbl == null)
            {
                return "0";
            }
            else
            {
                var name = asmbl.GetName();
                return name.Version.ToString();
            }
        }

        /// <summary>
        /// ストレージにJSON情報を保存する
        /// </summary>
        /// <param name="json">JSON情報</param>
        /// <exception cref="InvalidOperationException">ファイル保存失敗</exception>
        public static void SaveJsonToStorage(NbJsonObject json)
        {
            // ストレージ内情報を削除
            DeleteStorage();

            // ファイルに保存する
            try
            {
                System.IO.File.WriteAllText(InstallationFilename, json.ToString());
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Failed to SaveJsonToStorage()");
            }
        }

        /// <summary>
        /// ストレージからJsonを読み出す
        /// </summary>
        /// <returns>読み出した値</returns>
        public static NbJsonObject GetJsonFromStorage()
        {
            var jsonString = System.IO.File.ReadAllText(InstallationFilename);
            return NbJsonObject.Parse(jsonString);
        }

        /// <summary>
        /// NbHttpException発生時にResponseからエラー情報を取得する
        /// </summary>
        /// <param name="response">Response情報</param>
        /// <param name="error">Responseから情報を取得するキー</param>
        /// <returns>エラー情報</returns>
        public static string GetErrorInfo(HttpResponseMessage response, string error = "error")
        {
            if (response.Content != null)
            {
                var bodyString = response.Content.ReadAsStringAsync().Result;
                var responseJson = NbJsonObject.Parse(bodyString);
                return (string)responseJson[error];
            }
            return null;
        }

        /// <summary>
        /// UNIX Time (1970/1/1 00:00:00 UTC からの経過秒数) を返す
        /// </summary>
        /// <returns>UNIX Time</returns>
        public static long CurrentUnixTime()
        {
            long now = (long)((DateTime.UtcNow - EpochDate).TotalSeconds);
            return now;
        }

        /// <summary>
        /// インスタレーション登録/更新 -  任意の情報あり - 未ログイン
        /// </summary>
        public static async Task<NbSsePushInstallation> UpsertInstallation()
        {
            // Not SignUp & Login

            NbSsePushInstallation currentInstallation = NbSsePushInstallation.GetCurrentInstallation();

            // Channels Set
            ISet<string> channels = new HashSet<string>();
            channels.Add("chan1");
            currentInstallation.Channels = channels;

            // AllowedSenders Set
            ISet<string> allowedSenders = new HashSet<string>();
            allowedSenders.Add("g:anonymous");
            currentInstallation.AllowedSenders = allowedSenders;

            // Options Set
            NbJsonObject options = new NbJsonObject();
            options.Add("email", "ssetest@pushtest.com");
            options.Add("test", "testValue");
            currentInstallation.Options = options;

            // Main
            return await currentInstallation.Save();
        }

        // SSE Pushサーバからのレスポンスのうち、各テストで共通の部分をチェックする
        public static void CheckCommonResponse(NbSsePushInstallation installation)
        {
            //Channels, AllowedSenders, Owner, Options以外
            Assert.IsNotNullOrEmpty(installation.InstallationId);
            Assert.AreEqual("dotnet", installation.OsType);
            Assert.AreEqual("Unknown", installation.OsVersion);
            Assert.IsNotNullOrEmpty(installation.DeviceToken);
            Assert.AreEqual("sse", installation.PushType);
            Assert.AreEqual(-1, installation.AppVersionCode);
            Assert.AreEqual(ITUtil.GetVersionName(), installation.AppVersionString);
            Assert.IsNotNullOrEmpty(installation.Username);
            Assert.IsNotNullOrEmpty(installation.Password);
            StringAssert.Contains(TestConfig.SsePushEndpointUrl, installation.Uri);
        }

        // インスタレーション情報を格納するファイルが保存されたかどうかチェック
        public static void CheckSaveStorage(NbSsePushInstallation installationForCompare)
        {
            try
            {
                System.IO.File.ReadAllText(InstallationFilename);
            }
            catch (System.IO.FileNotFoundException)
            {
                Assert.Fail("Storage Not Found");
            }

            // ストレージに保存されたインスタレーション内容が正しいかどうかチェック
            NbSsePushInstallation installation = new NbSsePushInstallation();

            CheckCommonResponse(installation);
            Assert.AreEqual(installationForCompare.Channels, installation.Channels);
            Assert.AreEqual(installationForCompare.AllowedSenders, installation.AllowedSenders);
            Assert.AreEqual(installationForCompare.Owner, installation.Owner);
            Assert.AreEqual(installationForCompare.Options, installation.Options);
        }
    }
}
