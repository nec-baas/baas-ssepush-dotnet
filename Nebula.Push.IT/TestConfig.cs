namespace Nec.Nebula.IT
{
    /// <summary>
    /// テストサーバ設定
    /// </summary>
    class TestConfig
    {
        // BaaSサーバ URL
        public const string NebulaEndpointUrl = "https://baas.example.com/api";
        
        // SSE Pushサーバ
        public const string SsePushEndpointUrl = "https://baas.example.com/ssepush/events/";

        // テナントID or テナント名
        public const string TenantId = "";

        // クライアントPush有効アプリ
        public const string AppId = "";
        public const string AppKey = "";
        public const string MasterKey = "";

        // クライアントPush無効アプリ
        public const string AppIdForPushDisabled = "";
        public const string AppKeyForPushDisabled = "";
        public const string MasterKeyForPushDisabled = "";
    }
}
