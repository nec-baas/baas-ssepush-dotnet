using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Nec.Nebula.Internal;
using System.Diagnostics;
using SsePushClient;

namespace Nec.Nebula
{
    /// <summary>
    /// Server Sent Events(SSE) Pushメッセージ受信クライアントクラス。
    /// SSE Pushサーバへの接続(Pushメッセージ受信)/切断機能を提供する。
    /// </summary>
    public class NbSsePushReceiveClient
    {
        /// <summary>
        /// メッセージ受信用デリゲート関数
        /// </summary>
        /// <param name="message">受信メッセージ</param>
        public delegate void OnMessage(SseMessage message);
        
        /// <summary>
        /// エラー受信用デリゲート関数
        /// </summary>
        /// <param name="statusCode">HTTPステータスコード</param>
        /// <param name="errorInfo">エラーメッセージ</param>
        public delegate void OnError(HttpStatusCode statusCode, HttpWebResponse errorInfo);

        /// <summary>
        /// 接続完了通知用デリゲート関数
        /// </summary>
        public delegate void OnOpen();

        /// <summary>
        /// 切断完了通知用デリゲート関数
        /// </summary>
        public delegate void OnClose();

        /// <summary>
        /// NbService
        /// </summary>
        internal NbService Service { get; set; }

        /// <summary>
        /// 排他制御用セマフォ
        /// </summary>
        private static Semaphore _pool = new Semaphore(1, 1);

        internal SsePushReceiveClient _sseClient;

        internal enum State
        {
            // SSEサーバと切断状態。自動再接続も実施中ではない。
            Idle,
            // SSEサーバと接続中か接続状態。
            Connect
        };

        // SSEサーバとの接続状態
        internal State _clientState;

        // インスタレーション内のSSE関連情報
        private string _username;
        private string _password;
        private string _uri;

        // 排他制御用オブジェクト
        private static readonly object _lock = new object();

        // ネットワーク接続状態検知イベント登録済フラグ
        private bool _isNWChangedEventRegistered = false;

        // 自動回復処理実行済フラグ
        private int _autoRecoveryExecuteCount = 0;

        private OnError _onErrorCallback = null;
        private OnOpen _onOpenCallback = null;
        private OnClose _onCloseCallback = null;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public NbSsePushReceiveClient()
        {
            // インスタレーション情報取得
            LoadCurrentInstallation(); 

            // SsePushClient作成
            this._sseClient = new SsePushReceiveClient(this._uri);

            // 各コールバック登録
            RegisterCallbacks();

            // SSEサーバと切断状態に設定
            SetState(State.Idle);
        }

        /// <summary>
        /// インスタレーションの更新権利を取得する。
        /// 
        /// 同スレッド/他スレッドに関わらず2回の AcquireLock は不可(エラーを返す)
        /// 更新が終わったら、ReleaseLock()を呼ぶ必要がある。
        /// </summary>
        /// <exception cref="InvalidOperationException">更新権利取得失敗</exception>
        public static void AcquireLock()
        {
            Debug.WriteLine("NbSsePushReceiveClient: AcquireLock() <start>");

            lock (_lock)
            {
                // ロック取得(ブロッキングはしない)
                if (!_pool.WaitOne(0))
                {
                    // ロックを取得できない場合はエラーを返す
                    throw new InvalidOperationException("can not acquire lock.");
                }
            }

            Debug.WriteLine("NbSsePushReceiveClient: AcquireLock() <end>");
        }

        /// <summary>
        /// インスタレーションの更新権利を破棄する
        /// 
        /// AcquireLock したスレッド以外からでも ReleaseLock 可能
        /// </summary>
        public static void ReleaseLock()
        {
            Debug.WriteLine("NbSsePushReceiveClient: ReleaseLock() <start>");

            try
            {
                _pool.Release();
            }
            catch (SemaphoreFullException)
            {
                // ロック破棄済の場合
                // 特にエラーとはしない。
            }

            Debug.WriteLine("NbSsePushReceiveClient: ReleaseLock() <end>");
        }

        /// <summary>
        /// SSEサーバと接続する
        /// </summary>
        /// <exception cref="NbHttpException">リクエスト送信失敗</exception>
        /// <exception cref="InvalidOperationException">インスタレーションが存在しない</exception>
        public void Connect()
        {
            Debug.WriteLine("NbSsePushReceiveClient: Connect() <start>");

            if (GetState() != State.Idle)
            {
                Debug.WriteLine("NbSsePushReceiveClient: Connect() State is not idle. state=" + GetState());
                throw new NbHttpException(HttpStatusCode.BadRequest, "Connection is already being processed.");
            }

            // インスタレーション情報取得
            NbSsePushInstallation installation = LoadCurrentInstallation(); 

            // SSE情報チェック
            if (!HasSseInfo(installation))
            {
                Debug.WriteLine("NbSsePushReceiveClient: Connect() SSE Infomation doesn't exist in Installation.");
                throw new NbHttpException(HttpStatusCode.BadRequest, "SSE Infomation doesn't exist in Installation.");
            }

            // ネットワーク接続状態検知イベント登録
            RegisterNetworkChangedEvent();

            // SSEサーバと接続
            ConnectToPushClient();

            Debug.WriteLine("NbSsePushReceiveClient: Connect() <end>");
        }

        // ネットワーク接続状態検知イベントを登録する
        private void RegisterNetworkChangedEvent()
        {
            // 未登録時のみ登録
            lock (this)
            {
                if (!_isNWChangedEventRegistered)
                {
                    //NetworkAvailabilityChangedイベントハンドラを追加
                    NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;

                    // 登録済フラグを true にセット
                    _isNWChangedEventRegistered = true;
                }
            }
        }

        // ネットワーク接続状態検知イベントを登録解除する
        private void UnRegisterNetworkChangedEvent()
        {
            // 登録済の場合のみ解除
            lock (this)
            {
                if (_isNWChangedEventRegistered)
                {
                    NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;

                    // 登録済フラグを false にセット
                    _isNWChangedEventRegistered = false;
                }
            }
        }

        // ネットワーク接続状態検知時の処理
        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            // NW接続検知時
            if (e.IsAvailable && GetState() == State.Idle)
            {
                // 再接続
                Connect();
            }
            // NW切断検知時
            else if (!e.IsAvailable && GetState() == State.Connect) 
            {
                // 切断
                DisconnectToPushClient();
            }
        }

        // SSEサーバと接続する
        private void ConnectToPushClient()
        {
            // 接続中状態に設定
            SetState(State.Connect);
            _sseClient.Open(_username, _password);
        }

        /// <summary>
        /// SSEサーバと切断する
        /// </summary>
        public void Disconnect()
        {
            Debug.WriteLine("NbSsePushReceiveClient: Disconnect() <start>");

            // SSEサーバと切断
            DisconnectToPushClient();

            // ネットワーク接続状態検知イベントの登録解除
            UnRegisterNetworkChangedEvent();

            Debug.WriteLine("NbSsePushReceiveClient: Disconnect() <end>");
        }

        // SSEサーバと切断する
        private void DisconnectToPushClient()
        {
            _sseClient.Close();

            // SSEサーバと切断状態に設定
            SetState(State.Idle);
        }

        /// <summary>
        /// SSE Pushサーバからメッセージ受信時の処理を登録。
        /// 
        /// 受信するイベント名は"message"が設定される。
        /// </summary>
        /// <param name="Callback">メッセージ受信時の処理</param>
        public void RegisterOnMessage(OnMessage Callback)
        {
            RegisterOnMessage("message", Callback);
        }

        /// <summary>
        /// SSE Pushサーバからメッセージ受信時の処理を登録
        /// </summary>
        /// <param name="eventType">受信するイベント名</param>
        /// <param name="Callback">メッセージ受信時の処理</param>
        public void RegisterOnMessage(string eventType, OnMessage Callback)
        {
            Debug.WriteLine("NbSsePushReceiveClient: RegisterOnMessage() <start>");

            if (eventType == null)
            {
                throw new InvalidOperationException("Null EventType.");
            }

            if (Callback == null)
            {
                throw new InvalidOperationException("Null Callback.");
            }

            // SsePushClientにデリゲート関数登録
            _sseClient.RegisterEvent(eventType, (msg) => Callback(msg));

            Debug.WriteLine("NbSsePushReceiveClient: RegisterOnMessage() <end>");
        }

        /// <summary>
        /// SSE Pushサーバからエラー受信時の処理を登録
        /// </summary>
        /// <param name="Callback">エラー受信時の処理</param>
        public void RegisterOnError(OnError Callback)
        {
            _onErrorCallback = Callback;
        }

        // 自動回復処理
        internal bool AutoRecovery(HttpStatusCode statusCode, HttpWebResponse response)
        {
            try
            {
                // 排他ロック
                AcquireLock();
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine("NbSsePushReceiveClient: Auto recovery fails because of lock");
                return false;
            }

            // インスタレーション(SSE情報含む)の存在チェック
            if (!IsInstallationExists())
            {
                Debug.WriteLine("NbSsePushReceiveClient: Auto recovery fails because Installation doesn't exist.");

                ReleaseLock();
                return false;
            }
            
            // 自動回復処理を実施する
            try
            {
                // インスタレーション再登録
                var installation = NbSsePushInstallation.GetCurrentInstallation().Save().Result;

                ReleaseLock();

                // SSEサーバへ再接続
                _sseClient.Open(installation.Username, installation.Password);
                return true;
            }
                // Save()失敗時の処理。Open()のエラーはデリゲート関数で通知される。
            catch (Exception)
            {
                Debug.WriteLine("NbSsePushReceiveClient: Auto recovery fails because Installation can't be Registered.");

                ReleaseLock();
                return false;
            }
        }

        /// <summary>
        /// SSE Pushサーバから接続完了通知受信時の処理を登録
        /// </summary>
        /// <param name="Callback">接続完了通知受信時の処理</param>
        public void RegisterOnOpen(OnOpen Callback)
        {
            _onOpenCallback = Callback;
        }

        /// <summary>
        /// SSE Pushサーバから切断完了通知受信時の処理を登録
        /// </summary>
        /// <param name="Callback">切断完了通知受信時の処理</param>
        public void RegisterOnClose(OnClose Callback)
        {
            _onCloseCallback = Callback;
        }

        // 各コールバック登録
        private void RegisterCallbacks()
        {
            // Messageコールバック登録はここでは行わない

            // Errorコールバック登録
            _sseClient.RegisterOnError(HandleError);

            // Openコールバック登録
            _sseClient.RegisterOnOpen(() =>
            {
                _autoRecoveryExecuteCount = 0;

                var cb = _onOpenCallback;
                if (cb != null)
                {
                    cb();
                }
            });

            // Closeコールバック登録
            _sseClient.RegisterOnClose(() =>
            {
                // SSEサーバと切断状態に設定
                SetState(State.Idle);

                var cb = _onCloseCallback;
                // Callback実行
                if (cb != null)
                {
                    cb();
                }
            });
        }

        // エラー処理
        private void HandleError(HttpStatusCode statusCode, HttpWebResponse response)
        {
            // 認証エラー1回目の場合
            if (statusCode == HttpStatusCode.Unauthorized && _autoRecoveryExecuteCount == 0)
            {
                if (!AutoRecovery(statusCode, response))
                {
                    // 自動回復処理失敗時はエラーコールバック実行
                    ExecuteErrorCallback(statusCode, response);
                }
                else
                {
                    _autoRecoveryExecuteCount++;
                }
            }
            // 認証エラー以外、もしくは認証エラー2回目の場合
            else
            {
                _autoRecoveryExecuteCount = 0;

                // エラーコールバック実行
                ExecuteErrorCallback(statusCode, response);
            }
        }

        /// <summary>
        /// エラーコールバック実行
        /// </summary>
        /// <param name="statusCode">Httpステータスコード</param>
        /// <param name="response">Httpレスポンス</param>
        private void ExecuteErrorCallback(HttpStatusCode statusCode, HttpWebResponse response)
        {
            var cb = _onErrorCallback;
            if (cb != null)
            {
                cb(statusCode, response);
            }
        }

        // 接続状態を設定する
        private void SetState(State state)
        {
            this._clientState = state;
        }

        // 接続状態を取得する
        private State GetState()
        {
            return this._clientState;
        }

        // 端末ストレージに保存されているインスタレーション情報を取得し、SSE情報を保持する
        private NbSsePushInstallation LoadCurrentInstallation()
        {
            NbSsePushInstallation installation =  NbSsePushInstallation.GetCurrentInstallation();

            NbUtil.NotNullWithInvalidOperation(installation.InstallationId, "Null installationId");

            // SSE情報を保持
            this._username = installation.Username;
            this._password = installation.Password;
            this._uri = installation.Uri;

            return installation;
        }

        // インスタレーション内にSSE情報(username, password, uri)が存在するかどうかチェック
        private bool HasSseInfo(NbSsePushInstallation installation)
        {
            if (!string.IsNullOrEmpty(installation.Username) &&
                !string.IsNullOrEmpty(installation.Password) &&
                !string.IsNullOrEmpty(installation.Uri))
            {
                return true;
            }
            return false;
        }

        // インスタレーション情報(SSE情報含む)が存在するかどうかチェック
        private bool IsInstallationExists()
        {
            NbSsePushInstallation installation = NbSsePushInstallation.GetCurrentInstallation();

            if (!string.IsNullOrEmpty(installation.InstallationId) && HasSseInfo(installation))
            {
                return true;
            }
            
            return false;
        }
    }
}
