using System.Diagnostics;
using System.Text;
using System.Web;

namespace CgiHandler
{
    public class CgiHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            StringBuilder outHead;
            Process proc = PrepareProcess(context, out outHead);

            string cookies = GetCookies(context);

            StartProcess(proc, cookies);

            bool isHead;
            bool isRedirect;
            string redirectURL;
            byte[] outBody;
            int exitCode = ExirProcess(context, proc, outHead, out isHead, out isRedirect, out redirectURL, out outBody);

            OutputToWire(context, exitCode, isHead, isRedirect, redirectURL, outBody);
        }

        private int ExirProcess(HttpContext context, Process proc, StringBuilder outHead, out bool isHead, out bool isRedirect, out string redirectURL, out byte[] outBody)
        {
            Encoding stdoutEncoding = proc.StandardOutput.CurrentEncoding;

            // Read cgi input
            int inputByte = context.Request.InputStream.ReadByte();
            while (inputByte != -1)
            {
                proc.StandardInput.BaseStream.WriteByte((byte)inputByte);
                inputByte = context.Request.InputStream.ReadByte();
            }
            proc.StandardInput.Flush();
            proc.StandardInput.Close();

            isHead = ProcessCgiOutput(context, proc, outHead, out isRedirect, out redirectURL);

            outBody = proc.StandardOutput.CurrentEncoding.GetBytes(proc.StandardOutput.ReadToEnd());
            string procStdErr = proc.StandardError.ReadToEnd();

            proc.StandardError.Close();
            proc.StandardOutput.Close();
            proc.WaitForExit(3000);
            int exitCode = proc.ExitCode;
            proc.Close();
            return exitCode;
        }

        private Process PrepareProcess(HttpContext context, out StringBuilder outHead)
        {
            string pOutput;
            outHead = new StringBuilder(100);
            var proc = new Process
                           {
                               StartInfo =
                                   {
                                       FileName = "/Cgi/Path",
                                       WorkingDirectory = HttpRuntime.AppDomainAppPath,
                                       Arguments = GetScript(context) + " " + context.Request.QueryString
                                   }
                           };
            //p.StartInfo.Arguments = "-d:ptkdb " + script_name + " " + context.Request.QueryString.ToString();
            proc.StartInfo.EnvironmentVariables["CONTENT_LENGTH"] = context.Request.ContentLength.ToString();
            proc.StartInfo.EnvironmentVariables["REQUEST_METHOD"] = context.Request.HttpMethod;
            proc.StartInfo.EnvironmentVariables["CONTENT_TYPE"] = context.Request.ContentType;
            proc.StartInfo.EnvironmentVariables["QUERY_STRING"] = context.Request.QueryString.ToString();
            proc.StartInfo.EnvironmentVariables["SCRIPT_NAME"] = GetScript(context);
            proc.StartInfo.EnvironmentVariables["PATH_INFO"] = "/";
            proc.StartInfo.EnvironmentVariables["HTTP_USER_AGENT"] = context.Request.UserAgent;
            proc.StartInfo.EnvironmentVariables["REMOTE_ADDR"] = context.Request.UserHostAddress;

            foreach (string serverVariableKey in context.Request.ServerVariables.AllKeys)
            {
                proc.StartInfo.EnvironmentVariables[serverVariableKey] =
                    context.Request.ServerVariables[serverVariableKey];
            }
            return proc;
        }

        private void StartProcess(Process proc, string cookies)
        {
            proc.StartInfo.EnvironmentVariables["COOKIE"] = cookies;
            proc.StartInfo.EnvironmentVariables["HTTP_COOKIE"] = cookies;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
        }

        private string GetCookies(HttpContext context)
        {
            string cookies = "";
            HttpCookieCollection cookieCollection = context.Request.Cookies;

            if (cookieCollection != null)
            {
                foreach (string cookieName in cookieCollection)
                {
                    HttpCookie cookie = cookieCollection[cookieName];
                    if (cookie != null)
                    {
                        string value = cookie.Value;
                        cookies += string.Format("{0}={1};", cookieName, value);
                    }
                }
            }
            return cookies;
        }

        private static bool ProcessCgiOutput(HttpContext context, Process proc, StringBuilder outHead, out bool isRedirect, out string redirectURL)
        {
            string pOutput;
            redirectURL = "";
            isRedirect = false;

            bool isHead = true;

            while (true)
            {
                pOutput = proc.StandardOutput.ReadLine();
                if (pOutput == null) break;
                if (pOutput == "") { isHead = false; break; }

                outHead.AppendLine(pOutput);
                int headerSplitIndex = pOutput.IndexOf(':');

                if (headerSplitIndex < 0) continue;

                string headerName = pOutput.Substring(0, headerSplitIndex).Trim().ToLower();
                string headerValue = pOutput.Substring(headerSplitIndex + 1).Trim();

                if (headerName.Equals("content-type"))
                    context.Response.ContentType = headerValue;
                else if (headerName.IndexOf("set-cookie") == 0)
                {
                    string[] cs = headerValue.Split(';');
                    string[] ckv = cs[0].Split('=');
                    if (ckv.Length >= 2)
                    {
                        var c = new HttpCookie(ckv[0], ckv[1]);
                        context.Response.Cookies.Add(c);
                    }
                }
                else if (headerName.Equals("location"))
                {
                    isRedirect = true;
                    redirectURL = headerValue;
                }
                else
                    context.Response.AddHeader(headerName, headerValue);
            }
            return isHead;
        }

        private static void OutputToWire(HttpContext context, int exitCode, bool isHead, bool isRedirect, string redirectURL, byte[] outBody)
        {
            if (exitCode != 0 || isHead)
            {
                context.Response.Write("CGI Error");
                context.Response.End();
            }

            if (isRedirect)
            {
                context.Response.ClearContent();
                context.Response.Redirect(redirectURL, true);
            }

            if (outBody.Length > 0)
                context.Response.OutputStream.Write(outBody, 0, outBody.Length - 1);

            context.Response.End();
        }

        private static string GetScript(HttpContext context)
        {
            string script = context.Request.FilePath;
            script = script.Remove(0, context.Request.ApplicationPath.Length);
            if (script.IndexOf("/") == 0) script = script.Remove(0, 1);
            return script;
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }

    }
}