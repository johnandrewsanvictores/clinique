    using Microsoft.Web.WebView2.Core;
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Windows.Forms;

    namespace Queue_System
    {
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer queueRefreshTimer;
        private string connectionString = @"Server=.\SQLEXPRESS;Database=QueueSystemDB;Trusted_Connection=True;";  //original
        //private string connectionString = @"Server=localhost\SQLEXPRESS;Database=QueueSystemDB;Trusted_Connection=True;";

        public Form1()
        {
            InitializeComponent();
            InitializeWebView();
            this.WindowState = FormWindowState.Maximized;
            webView21.Dock = DockStyle.Fill;

        }
        
        private void InitializeWebView()
        {
            webView21.EnsureCoreWebView2Async(null).ContinueWith(t =>
            {
                if (t.Exception == null)
                {
                    this.Invoke((Action)(() =>
                    {
                        string splashPath = Path.Combine(Application.StartupPath, "wwwroot", "splash.html");
                        string splashUri = $"file:///{splashPath.Replace("\\", "/")}";
                        webView21.Source = new Uri(splashUri);

                        webView21.CoreWebView2.WebMessageReceived += WebMessageReceived;
                    }));
                }
                else
                {
                    MessageBox.Show("WebView2 initialization failed: " + t.Exception.Message);
                }
            });
        }

        private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    
                    if (message == "goLogin")
                    {
                        NavigateTo("login.html");
                    }
                    else if (message.StartsWith("login:"))
                    {
                        string[] parts = message.Split(':');
                        string username = parts.Length > 1 ? parts[1] : "";
                        string password = parts.Length > 2 ? parts[2] : "";
                        string role = parts.Length > 3 ? parts[3] : "";

                        string query = "SELECT Id FROM Users WHERE Username=@user AND Password=@pass AND Role=@role";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", username);
                            cmd.Parameters.AddWithValue("@pass", password);
                            cmd.Parameters.AddWithValue("@role", role);

                            object result = cmd.ExecuteScalar();
                            if (result != null)
                            {
                                int userId = Convert.ToInt32(result);

                                // Check if user has previous login or re-login
                                string checkLoginQuery = "SELECT COUNT(*) FROM LoginHistory WHERE UserId=@id AND Action IN ('Login','Re-Login')";
                                using (SqlCommand checkCmd = new SqlCommand(checkLoginQuery, conn))
                                {
                                    checkCmd.Parameters.AddWithValue("@id", userId);
                                    int count = (int)checkCmd.ExecuteScalar();

                                    // Decide action: Login if first time, Re-Login if not
                                    string action = count == 0 ? "Login" : "Re-Login";

                                    // Insert the determined action
                                    string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (@id, @user, @action)";
                                    using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                                    {
                                        logCmd.Parameters.AddWithValue("@id", userId);
                                        logCmd.Parameters.AddWithValue("@user", username);
                                        logCmd.Parameters.AddWithValue("@action", action);
                                        logCmd.ExecuteNonQuery();
                                    }
                                }

                                webView21.CoreWebView2.PostWebMessageAsString($"setUser:{username}:{role}:{userId}");
                                SendMessage($"{role} Login Successful!");
                            }
                            else
                            {
                                SendMessage("Invalid credentials or role", true);
                            }
                        }
                    }

                    else if (message.StartsWith("goModule:"))
                    {
                        string role = message.Split(':')[1];
                        NavigateTo(role == "admin" ? "admin.html" : "staff.html");
                    }
                    else if (message.StartsWith("createStaff:"))
                    {
                        string[] parts = message.Split(':');
                        string username = parts.Length > 1 ? parts[1] : "";
                        string password = parts.Length > 2 ? parts[2] : "";
                        string role = "staff";

                        string insertQuery = "INSERT INTO Users (Username, Password, Role) VALUES (@user,@pass,@role)";
                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", username);
                            cmd.Parameters.AddWithValue("@pass", password);
                            cmd.Parameters.AddWithValue("@role", role);
                            cmd.ExecuteNonQuery();
                        }

                        // Log Account Created
                        string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) " +
                                          "VALUES ((SELECT Id FROM Users WHERE Username=@user), @user, 'Account Created')";
                        using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                        {
                            logCmd.Parameters.AddWithValue("@user", username);
                            logCmd.ExecuteNonQuery();
                        }

                        SendMessage($"Staff '{username}' created successfully!");
                    }
                    else if (message == "manageAccounts")
                    {
                        string selectQuery = "SELECT Id, Username, Password, Role FROM Users";
                        using (SqlDataAdapter da = new SqlDataAdapter(selectQuery, conn))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dt);
                            webView21.CoreWebView2.PostWebMessageAsString("accounts:" + json);
                        }
                    }


                    else if (message.StartsWith("updateAccount:"))
                    {
                        string[] parts = message.Split(':');
                        int id = Convert.ToInt32(parts[1]);
                        string username = parts[2];
                        string password = parts[3];

                        string updateQuery = "UPDATE Users SET Username=@user, Password=@pass WHERE Id=@id";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", username);
                            cmd.Parameters.AddWithValue("@pass", password);
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }

                        // Log Account Modified
                        string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (@id,@user,'Account Modified')";
                        using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                        {
                            logCmd.Parameters.AddWithValue("@id", id);
                            logCmd.Parameters.AddWithValue("@user", username);
                            logCmd.ExecuteNonQuery();
                        }

                        SendMessage($"Account '{username}' updated successfully!");
                    }
                    else if (message.StartsWith("deleteAccount:"))
                    {
                        int id = Convert.ToInt32(message.Split(':')[1]);

                        // Get username before delete
                        string getUserQuery = "SELECT Username FROM Users WHERE Id=@id";
                        string username = "";
                        using (SqlCommand getCmd = new SqlCommand(getUserQuery, conn))
                        {
                            getCmd.Parameters.AddWithValue("@id", id);
                            var result = getCmd.ExecuteScalar();
                            if (result != null) username = result.ToString();
                        }

                        // Log Account Deleted
                        if (!string.IsNullOrEmpty(username))
                        {
                            string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (@id,@user,'Account Deleted')";
                            using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                            {
                                logCmd.Parameters.AddWithValue("@id", id);
                                logCmd.Parameters.AddWithValue("@user", username);
                                logCmd.ExecuteNonQuery();
                            }
                        }

                        // Delete account
                        string deleteQuery = "DELETE FROM Users WHERE Id=@id";
                        using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }

                        SendMessage("Account deleted successfully!");
                    }
                    else if (message.StartsWith("viewLoginHistory"))
                    {
                        string selectQuery = "SELECT Username, Action, CONVERT(VARCHAR, ActionTime, 23) AS Date, CONVERT(VARCHAR, ActionTime, 8) AS Time " +
                                             "FROM LoginHistory ORDER BY ActionTime DESC";
                        using (SqlDataAdapter da = new SqlDataAdapter(selectQuery, conn))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dt);
                            webView21.CoreWebView2.PostWebMessageAsString("loginHistory:" + json);
                        }
                    }
                    else if (message == "clearLoginHistory")
                    {
                        string deleteQuery = "DELETE FROM LoginHistory";
                        using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        SendMessage("Login history cleared successfully!");
                    }

                    else if (message.StartsWith("resetActivity:"))
                    {
                        int staffId = Convert.ToInt32(message.Split(':')[1]);
                        string updateQuery = "UPDATE Queue SET Status='Pending' WHERE StaffId=@id AND Status='Served'";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", staffId);
                            cmd.ExecuteNonQuery();
                        }

                        SendMessage("Activity reset successfully!");
                    }
                    else if (message == "logout")
                    {
                        // Get current logged-in user
                        string username = "";
                        string roleQuery = "SELECT TOP 1 Username FROM LoginHistory WHERE Action='Login' ORDER BY ActionTime DESC";
                        using (SqlCommand cmd = new SqlCommand(roleQuery, conn))
                        {
                            var result = cmd.ExecuteScalar();
                            if (result != null)
                                username = result.ToString();
                        }

                        // Log Logout
                        if (!string.IsNullOrEmpty(username))
                        {
                            string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) " +
                                              "VALUES ((SELECT Id FROM Users WHERE Username=@user), @user, 'Logout')";
                            using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                            {
                                logCmd.Parameters.AddWithValue("@user", username);
                                logCmd.ExecuteNonQuery();
                            }
                        }

                        NavigateTo("login.html");
                    }
                    else if (message.StartsWith("newQueue:"))
                    {
                        // Parse JSON to get staff username
                        string json = message.Substring("newQueue:".Length);
                        dynamic queueData = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                        string staffUsername = queueData.staff;
                        Console.WriteLine("Staff Username for new queue: " + staffUsername);
                        string updateQuery = "UPDATE Users SET total_number_generated = ISNULL(total_number_generated,0) +1 WHERE Username = @user";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", staffUsername);
                            cmd.ExecuteNonQuery();
                        }


                        string number = queueData.number;
                        string service = queueData.service;
                        string datetimeStr = queueData.datetime;

                        int staffId = 0;
                        string getStaffIdQuery = "SELECT Id FROM Users WHERE Username = @user";
                        using (SqlCommand getCmd = new SqlCommand(getStaffIdQuery, conn))
                        {
                            getCmd.Parameters.AddWithValue("@user", staffUsername);
                            var result = getCmd.ExecuteScalar();
                            if (result != null) staffId = Convert.ToInt32(result);
                        }
                        string insertQuery = "INSERT INTO Queue (QueueNumber, Service, StaffId, CreatedAt, Status) VALUES (@number, @service, @staffId, @dateTime, @status)";
                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@number", number);
                            cmd.Parameters.AddWithValue("@service", service);
                            cmd.Parameters.AddWithValue("@staffId", staffId);
                            cmd.Parameters.AddWithValue("@dateTime", DateTime.Parse(datetimeStr));
                            cmd.Parameters.AddWithValue("@status", "Pending");
                            cmd.ExecuteNonQuery();
                        }

                        // Increment total_number_generated for this staff

                        // Optionally: You can log this action or send a confirmation message if needed
                        // Send success message
                        //SendMessage($"Queue #{number} created successfully!");
                    }
                   
else if (message.StartsWith("incrementActivity:"))
{
    // message: incrementActivity:<adminId>
    int adminId = Convert.ToInt32(message.Split(':')[1]);
    string updateQuery = "UPDATE Users SET total_number_generated = ISNULL(total_number_generated,0) +1 WHERE Id = @id";
    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
    {
        cmd.Parameters.AddWithValue("@id", adminId);
        cmd.ExecuteNonQuery();
    }
}
                    else if (message.StartsWith("getTotalGenerated:"))
                    {
                        // message: getTotalGenerated:<username>
                        string username = message.Substring("getTotalGenerated:".Length);
                        string selectQuery = "SELECT ISNULL(total_number_generated,0) FROM Users WHERE Username = @user";
                        using (SqlCommand cmd = new SqlCommand(selectQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", username);
                            object result = cmd.ExecuteScalar();
                            int total = result != null ? Convert.ToInt32(result) : 0;
                            webView21.CoreWebView2.PostWebMessageAsString($"totalGenerated:{username}:{total}");
                        }
                    }
                    else if (message.StartsWith("resetTotalGenerated:"))
                    {
                        // message: resetTotalGenerated:<username>
                        string username = message.Substring("resetTotalGenerated:".Length);
                        string updateQuery = "UPDATE Users SET total_number_generated =0 WHERE Username = @user";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", username);
                            cmd.ExecuteNonQuery();
                        }
                        // Notify frontend
                        webView21.CoreWebView2.PostWebMessageAsString($"totalGenerated:{username}:0");
                    }
                }
            }
            catch (Exception ex)
            {
                SendMessage("Error: " + ex.Message, true);
            }
        }



        private void NavigateTo(string filename)
        {
            string path = Path.Combine(Application.StartupPath, "wwwroot", filename);
            string uri = $"file:///{path.Replace("\\", "/")}";
            webView21.Source = new Uri(uri);
        }

        private void SendMessage(string message, bool isError = false)
        {
            string formattedMessage = isError ? $"error:{message}" : $"success:{message}";
            webView21.CoreWebView2.PostWebMessageAsString(formattedMessage);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void webView21_Click(object sender, EventArgs e)
        {

        }
    }
}
