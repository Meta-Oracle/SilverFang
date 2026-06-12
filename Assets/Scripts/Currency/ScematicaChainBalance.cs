using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace SilverFang.Currency
{
    /// Read-only lookup of a wallet's real SCEM balance on Solana mainnet via
    /// JSON-RPC (getTokenAccountsByOwner filtered to the Scematica mint).
    /// Display-only: no keys, no signing, no transactions.
    public class ScematicaChainBalance : MonoBehaviour
    {
        [SerializeField] private string rpcUrl = "https://api.mainnet-beta.solana.com";
        [Tooltip("Solana wallet public key to look up. Leave empty to disable.")]
        [SerializeField] private string walletAddress;

        public double? LastBalance { get; private set; }
        public event Action<double> OnBalanceFetched;

        public void Fetch()
        {
            if (string.IsNullOrEmpty(walletAddress)) return;
            StartCoroutine(FetchRoutine());
        }

        private IEnumerator FetchRoutine()
        {
            string body =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"getTokenAccountsByOwner\",\"params\":[\""
                + walletAddress + "\",{\"mint\":\"" + ScematicaToken.MintAddress
                + "\"},{\"encoding\":\"jsonParsed\"}]}";

            using (var req = new UnityWebRequest(rpcUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Scematica RPC failed: {req.error}");
                    yield break;
                }

                // Sum uiAmountString across the wallet's token accounts for this
                // mint. Plain string scan: the RPC response shape is too nested
                // for JsonUtility and this is the only field we need.
                string json = req.downloadHandler.text;
                const string key = "\"uiAmountString\":\"";
                double total = 0;
                int idx = 0;
                while ((idx = json.IndexOf(key, idx, StringComparison.Ordinal)) >= 0)
                {
                    idx += key.Length;
                    int end = json.IndexOf('"', idx);
                    if (end < 0) break;
                    if (double.TryParse(json.Substring(idx, end - idx), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double v))
                        total += v;
                    idx = end;
                }

                LastBalance = total;
                OnBalanceFetched?.Invoke(total);
            }
        }
    }
}
