    using Microsoft.Web.WebView2.Core;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
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

                                // Send setUser in the format expected by the frontend: ID:Username:Role
                                webView21.CoreWebView2.PostWebMessageAsString($"setUser:{userId}:{username}:{role}");
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

                        // Log Account Created (with NULL UserId to preserve history if account is deleted)
                        string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (NULL, @user, 'Account Created')";
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

                        // Log Account Modified (with NULL UserId to preserve history if account is deleted)
                        string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (NULL, @user, 'Account Modified')";
                        using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                        {
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

                        // Delete account first
                        string deleteQuery = "DELETE FROM Users WHERE Id=@id";
                        using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }

                        // Log Account Deleted after deletion (without UserId to avoid FK constraint issues)
                        if (!string.IsNullOrEmpty(username))
                        {
                            string logQuery = "INSERT INTO LoginHistory (UserId, Username, Action) VALUES (NULL, @user, 'Account Deleted')";
                            using (SqlCommand logCmd = new SqlCommand(logQuery, conn))
                            {
                                logCmd.Parameters.AddWithValue("@user", username);
                                logCmd.ExecuteNonQuery();
                            }
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
                        string serviceType = queueData.serviceType;
                        string datetimeStr = queueData.datetime;

                        int staffId = 0;
                        string getStaffIdQuery = "SELECT Id FROM Users WHERE Username = @user";
                        using (SqlCommand getCmd = new SqlCommand(getStaffIdQuery, conn))
                        {
                            getCmd.Parameters.AddWithValue("@user", staffUsername);
                            var result = getCmd.ExecuteScalar();
                            if (result != null) staffId = Convert.ToInt32(result);
                        }
                        string insertQuery = "INSERT INTO Queue (QueueNumber, Service, ServiceType, StaffId, CreatedAt, Status) VALUES (@number, @service, @serviceType, @staffId, @dateTime, @status)";
                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@number", number);
                            cmd.Parameters.AddWithValue("@service", service);
                            cmd.Parameters.AddWithValue("@serviceType", serviceType);
                            cmd.Parameters.AddWithValue("@staffId", staffId);
                            cmd.Parameters.AddWithValue("@dateTime", DateTime.Parse(datetimeStr));
                            cmd.Parameters.AddWithValue("@status", "Pending");
                            cmd.ExecuteNonQuery();
                        }

                        // Increment total_number_generated for this staff

                        // Optionally: You can log this action or send a confirmation message if needed
                        // Send updated queue list to frontend so admin can refresh automatically
                        try
                        {
                            string selectQuery = "SELECT Id, QueueNumber, Service, StaffId, CreatedAt, Status FROM Queue WHERE Status IN ('Pending','Called') ORDER BY CreatedAt ASC";
                            using (SqlDataAdapter da = new SqlDataAdapter(selectQuery, conn))
                            {
                                DataTable dt2 = new DataTable();
                                da.Fill(dt2);
                                string json2 = Newtonsoft.Json.JsonConvert.SerializeObject(dt2);
                                webView21.CoreWebView2.PostWebMessageAsString("queueList:" + json2);
                            }
                        }
                        catch
                        {
                            // Non-fatal: if broadcasting fails, just continue
                        }
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
                    else if (message == "getQueueList")
                    {
                        // Fetch pending/called queue entries from DB and send to frontend
                        string selectQuery = "SELECT Id, QueueNumber, Service, StaffId, CreatedAt, Status FROM Queue WHERE Status IN ('Pending','Called') ORDER BY CreatedAt ASC";
                        using (SqlDataAdapter da = new SqlDataAdapter(selectQuery, conn))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dt);
                            webView21.CoreWebView2.PostWebMessageAsString("queueList:" + json);
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
                    else if (message == "getPatients")
                    {
                        // Fetch all patients with their visits
                        string query = @"
                            SELECT 
                                p.PatientId, p.UniqueId, p.Name, p.Contact, p.Birthday, 
                                p.Weight, p.Height, p.Age, p.Notes,
                                v.VisitId, v.VisitDate, v.Purpose, v.IsFirstVisit, v.VisitNotes
                            FROM Patients p
                            LEFT JOIN Visits v ON p.PatientId = v.PatientId
                            ORDER BY p.PatientId DESC, v.VisitDate DESC";
                        
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            SqlDataReader reader = cmd.ExecuteReader();
                            var patientsDict = new Dictionary<int, dynamic>();
                            
                            while (reader.Read())
                            {
                                int patientId = reader.GetInt32(0);
                                
                                if (!patientsDict.ContainsKey(patientId))
                                {
                                    patientsDict[patientId] = new
                                    {
                                        id = patientId,
                                        uniqueId = reader.IsDBNull(1) ? null : reader.GetString(1),
                                        name = reader.GetString(2),
                                        contact = reader.GetString(3),
                                        birthday = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                                        weight = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                                        height = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                                        age = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                        notes = reader.IsDBNull(8) ? null : reader.GetString(8),
                                        visits = new List<dynamic>()
                                    };
                                }
                                
                                // Add visit if exists
                                if (!reader.IsDBNull(9))
                                {
                                    ((List<dynamic>)patientsDict[patientId].visits).Add(new
                                    {
                                        visitId = reader.GetInt32(9),
                                        date = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                                        purpose = reader.GetString(11),
                                        isFirstVisit = reader.GetBoolean(12),
                                        visitNotes = reader.IsDBNull(13) ? null : reader.GetString(13)
                                    });
                                }
                            }
                            reader.Close();
                            
                            var patientsList = patientsDict.Values.ToList();
                            string json = Newtonsoft.Json.JsonConvert.SerializeObject(patientsList);
                            webView21.CoreWebView2.PostWebMessageAsString("patients:" + json);
                        }
                    }
                    else if (message.StartsWith("addPatient:"))
                    {
                        string json = message.Substring("addPatient:".Length);
                        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        string name = data["name"]?.ToString() ?? "";
                        
                        // Check if patient with same name already exists
                        string checkQuery = "SELECT COUNT(*) FROM Patients WHERE Name = @name";
                        using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@name", name);
                            int existingCount = (int)checkCmd.ExecuteScalar();
                            
                            if (existingCount > 0)
                            {
                                webView21.CoreWebView2.PostWebMessageAsString($"error:A patient with the name '{name}' already exists in the system.");
                                return;
                            }
                        }
                        
                        // Generate UniqueId (e.g., DELACRUZ-001)
                        string lastName = name.Split(' ').LastOrDefault()?.ToUpper() ?? "PATIENT";
                        string uniqueId = GenerateUniquePatientId(conn, lastName);
                        
                        // Insert patient
                        string insertPatient = @"
                            INSERT INTO Patients (UniqueId, Name, Contact, Birthday, Weight, Height, Age, Notes, CurrentTime)
                            VALUES (@uniqueId, @name, @contact, @birthday, @weight, @height, @age, @notes, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() as int);";
                        
                        int patientId;
                        using (SqlCommand cmd = new SqlCommand(insertPatient, conn))
                        {
                            cmd.Parameters.AddWithValue("@uniqueId", uniqueId);
                            cmd.Parameters.AddWithValue("@name", name);
                            cmd.Parameters.AddWithValue("@contact", data["contact"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@birthday", data.ContainsKey("birthday") && data["birthday"] != null && !string.IsNullOrEmpty(data["birthday"].ToString()) ? (object)data["birthday"].ToString() : DBNull.Value);
                            cmd.Parameters.AddWithValue("@weight", data.ContainsKey("weight") && data["weight"] != null && !string.IsNullOrEmpty(data["weight"].ToString()) ? (object)Convert.ToDecimal(data["weight"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@height", data.ContainsKey("height") && data["height"] != null && !string.IsNullOrEmpty(data["height"].ToString()) ? (object)Convert.ToDecimal(data["height"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@age", data.ContainsKey("age") && data["age"] != null && !string.IsNullOrEmpty(data["age"].ToString()) ? (object)Convert.ToInt32(data["age"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@notes", data.ContainsKey("notes") && data["notes"] != null ? (object)data["notes"].ToString() : DBNull.Value);
                            patientId = (int)cmd.ExecuteScalar();
                        }
                        
                        // Validate visit date is today
                        string visitDateStr = data["visitDate"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(visitDateStr))
                        {
                            DateTime visitDate;
                            if (DateTime.TryParse(visitDateStr, out visitDate))
                            {
                                if (visitDate.Date != DateTime.Today)
                                {
                                    webView21.CoreWebView2.PostWebMessageAsString("error:Visit date must be today's date. Past or future dates are not allowed.");
                                    return;
                                }
                            }
                        }
                        
                        // Insert first visit
                        string insertVisit = @"
                            INSERT INTO Visits (PatientId, VisitDate, Purpose, IsFirstVisit, VisitNotes)
                            VALUES (@patientId, @visitDate, @purpose, @isFirstVisit, @visitNotes)";
                        
                        using (SqlCommand cmd = new SqlCommand(insertVisit, conn))
                        {
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@visitDate", DateTime.Today.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@purpose", data["visitPurpose"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@isFirstVisit", data.ContainsKey("isFirstVisit") && Convert.ToBoolean(data["isFirstVisit"]));
                            cmd.Parameters.AddWithValue("@visitNotes", data.ContainsKey("visitNotes") && data["visitNotes"] != null ? (object)data["visitNotes"].ToString() : DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                        
                        webView21.CoreWebView2.PostWebMessageAsString($"patientAdded:{patientId}");
                    }
                    else if (message.StartsWith("addVisit:"))
                    {
                        string json = message.Substring("addVisit:".Length);
                        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        // Validate visit date is today
                        string visitDateStr = data["visitDate"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(visitDateStr))
                        {
                            DateTime visitDate;
                            if (DateTime.TryParse(visitDateStr, out visitDate))
                            {
                                if (visitDate.Date != DateTime.Today)
                                {
                                    webView21.CoreWebView2.PostWebMessageAsString("error:Visit date must be today's date. Past or future dates are not allowed.");
                                    return;
                                }
                            }
                        }
                        
                        string insertVisit = @"
                            INSERT INTO Visits (PatientId, VisitDate, Purpose, IsFirstVisit, VisitNotes)
                            VALUES (@patientId, @visitDate, @purpose, @isFirstVisit, @visitNotes)";
                        
                        using (SqlCommand cmd = new SqlCommand(insertVisit, conn))
                        {
                            cmd.Parameters.AddWithValue("@patientId", Convert.ToInt32(data["patientId"]));
                            cmd.Parameters.AddWithValue("@visitDate", DateTime.Today.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@purpose", data["visitPurpose"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@isFirstVisit", Convert.ToBoolean(data["isFirstVisit"]));
                            cmd.Parameters.AddWithValue("@visitNotes", data.ContainsKey("visitNotes") && data["visitNotes"] != null ? (object)data["visitNotes"].ToString() : DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                        
                        webView21.CoreWebView2.PostWebMessageAsString("visitAdded:success");
                    }
                    else if (message.StartsWith("updatePatient:"))
                    {
                        string json = message.Substring("updatePatient:".Length);
                        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        int patientId = Convert.ToInt32(data["patientId"]);
                        string newName = data["name"]?.ToString() ?? "";
                        
                        string getCurrentNameQuery = "SELECT Name FROM Patients WHERE PatientId = @patientId";
                        string currentName = "";
                        using (SqlCommand cmd = new SqlCommand(getCurrentNameQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            var result = cmd.ExecuteScalar();
                            if (result != null) currentName = result.ToString();
                        }
                        
                        // Check if another patient with the same name already exists (excluding current patient)
                        if (currentName != newName)
                        {
                            string checkQuery = "SELECT COUNT(*) FROM Patients WHERE Name = @name AND PatientId != @patientId";
                            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                            {
                                checkCmd.Parameters.AddWithValue("@name", newName);
                                checkCmd.Parameters.AddWithValue("@patientId", patientId);
                                int existingCount = (int)checkCmd.ExecuteScalar();
                                
                                if (existingCount > 0)
                                {
                                    webView21.CoreWebView2.PostWebMessageAsString($"error:A patient with the name '{newName}' already exists in the system.");
                                    return;
                                }
                            }
                        }
                        
                        string currentLastName = currentName.Split(' ').LastOrDefault()?.ToUpper() ?? "";
                        string newLastName = newName.Split(' ').LastOrDefault()?.ToUpper() ?? "";
                        
                        string updateQuery = @"
                            UPDATE Patients 
                            SET Name = @name, Contact = @contact, Birthday = @birthday, 
                                Weight = @weight, Height = @height, Age = @age, Notes = @notes";
                        
                        if (currentLastName != newLastName && !string.IsNullOrEmpty(newLastName))
                        {
                            string newUniqueId = GenerateUniquePatientId(conn, newLastName);
                            updateQuery += ", UniqueId = @uniqueId";
                        }
                        
                        updateQuery += " WHERE PatientId = @patientId";
                        
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@name", newName);
                            cmd.Parameters.AddWithValue("@contact", data["contact"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@birthday", data.ContainsKey("birthday") && data["birthday"] != null && !string.IsNullOrEmpty(data["birthday"].ToString()) ? (object)data["birthday"].ToString() : DBNull.Value);
                            cmd.Parameters.AddWithValue("@weight", data.ContainsKey("weight") && data["weight"] != null && !string.IsNullOrEmpty(data["weight"].ToString()) ? (object)Convert.ToDecimal(data["weight"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@height", data.ContainsKey("height") && data["height"] != null && !string.IsNullOrEmpty(data["height"].ToString()) ? (object)Convert.ToDecimal(data["height"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@age", data.ContainsKey("age") && data["age"] != null && !string.IsNullOrEmpty(data["age"].ToString()) ? (object)Convert.ToInt32(data["age"]) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@notes", data.ContainsKey("notes") && data["notes"] != null ? (object)data["notes"].ToString() : DBNull.Value);
                            
                            if (currentLastName != newLastName && !string.IsNullOrEmpty(newLastName))
                            {
                                string newUniqueId = GenerateUniquePatientId(conn, newLastName);
                                cmd.Parameters.AddWithValue("@uniqueId", newUniqueId);
                            }
                            
                            cmd.ExecuteNonQuery();
                        }
                        
                        webView21.CoreWebView2.PostWebMessageAsString("patientUpdated:success");
                    }
                }
            }
            catch (Exception ex)
            {
                SendMessage("Error: " + ex.Message, true);
            }
        }

        private string GenerateUniquePatientId(SqlConnection conn, string lastName)
        {
            // Get the count of patients with the same last name
            string countQuery = "SELECT COUNT(*) FROM Patients WHERE UniqueId LIKE @pattern";
            using (SqlCommand cmd = new SqlCommand(countQuery, conn))
            {
                cmd.Parameters.AddWithValue("@pattern", lastName + "-%");
                int count = (int)cmd.ExecuteScalar();
                return $"{lastName}-{(count + 1):D3}";
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
