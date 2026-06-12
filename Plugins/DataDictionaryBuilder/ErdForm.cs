using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DataDictionaryBuilder
{
    /// <summary>
    /// A non-modal floating window that renders a Mermaid erDiagram in a WebView2.
    /// Mermaid.js is pulled from a CDN at render time.
    /// </summary>
    public class ErdForm : Form
    {
        private readonly WebView2 _web = new WebView2();
        private readonly string _mermaid;

        public ErdForm(string mermaid)
        {
            _mermaid = mermaid ?? string.Empty;

            Text = "Entity Relationship Diagram";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(960, 720);
            MinimumSize = new Size(400, 300);

            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);

            Load += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var userData = Path.Combine(Path.GetTempPath(), "XtbDataDictionaryErd");
                Directory.CreateDirectory(userData);

                var environment = await CoreWebView2Environment.CreateAsync(null, userData, null);
                await _web.EnsureCoreWebView2Async(environment);
                _web.NavigateToString(BuildHtml(_mermaid));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Unable to initialize the diagram view (WebView2 runtime required):\n\n" + ex.Message,
                    "ERD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildHtml(string mermaid)
        {
            var safe = mermaid
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

            return
@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body { font-family: 'Segoe UI', Arial, sans-serif; margin: 10px; }
  .mermaid { font-size: 14px; }
  #error { color: #b00; white-space: pre-wrap; font-family: Consolas, monospace; }
</style>
<script src='https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js'></script>
</head>
<body>
<div id='error'></div>
<pre class='mermaid'>
" + safe + @"
</pre>
<script>
  try {
    mermaid.initialize({ startOnLoad: true, securityLevel: 'loose' });
  } catch (e) {
    document.getElementById('error').textContent = 'Mermaid failed to load: ' + e;
  }
</script>
</body>
</html>";
        }
    }
}
