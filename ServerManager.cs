﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.IO;
using System.Web;
using Google.Apis.Sheets.v4.Data;
using System.Reflection;

namespace TTRPG_manager
{

    public class ServerManager
    {
        private AppConfig _config;

        
        private HttpListener listener;
        private bool isServerRunning = false;
        public bool StartServer()
        {
            _config = ConfigManager.LoadConfig();
            if (!_config.addedFirewallRule)
            {
                AddFirewallRuleWithUserConsent();
                _config.addedFirewallRule = true;
                ConfigManager.SaveConfig(_config);
            }
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:8080/");
            try
            {
                listener.Start();
                isServerRunning = true;
                Task.Run(() => HandleIncomingConnections());
                MessageBox.Show("Server started on port 8080.");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}");
                return false;
            }
        }
        private async Task HandleIncomingConnections()
        {
            while (isServerRunning)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest request = ctx.Request;
                HttpListenerResponse response = ctx.Response;
                string responseString = "";

                try
                {
                    if (request.Url.AbsolutePath.StartsWith("/images/"))
                    {
                        // Extract character name from URL
                        string characterName = request.Url.AbsolutePath.Substring("/images/".Length);
                        var character = _config.Parties[_config.selectedPartyIndex].Members.FirstOrDefault(c => c.Name.Equals(characterName, StringComparison.OrdinalIgnoreCase));
                        if (character != null && !string.IsNullOrEmpty(character.ImagePath) && File.Exists(character.ImagePath))
                        {
                            byte[] imageBytes = File.ReadAllBytes(character.ImagePath);
                            response.ContentType = "image/jpeg"; // Adjust based on the actual image format
                            response.ContentLength64 = imageBytes.Length;
                            await response.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                        }
                        else
                        {
                            response.StatusCode = 404; // Not Found
                        }
                    }
                    else if (request.HttpMethod == "POST")
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string postData = await reader.ReadToEndAsync();
                            var formData = HttpUtility.ParseQueryString(postData);

                            if (request.RawUrl.Contains("/updateCharacter"))
                            {
                                // Process the update form submission
                                string name = formData["Name"];
                                string description = formData["Description"];
                                int age = int.Parse(formData["Age"]); // Add error handling for parsing
                                string gender = formData["Gender"];
                                string race = formData["Race"];
                                string title = formData["Title"];
                                // Find and update the character
                                var characterToUpdate = _config.Parties[_config.selectedPartyIndex].Members.FirstOrDefault(c => c.Name == name);
                                if (characterToUpdate != null)
                                {
                                    characterToUpdate.Description = description;
                                    characterToUpdate.Age = age;
                                    characterToUpdate.Gender = gender;
                                    characterToUpdate.Race = race;
                                    characterToUpdate.Title = title;
                                    // Update other fields as needed
                                }
                                ConfigManager.SaveConfig(_config);

                                // Optionally redirect to a confirmation page or back to the form
                                response.Redirect("/editCharacter?name=" + HttpUtility.UrlEncode(name));

                            }
                            else
                            {
                                // Handling other POST requests if any
                                responseString = "Invalid request.";
                            }
                        }
                    }
                    else if (request.HttpMethod == "GET")
                    {
                        // Serve the form with the dropdown for character selection
                        // or editing if a specific character is selected
                        if (request.RawUrl.Contains("/editCharacter"))
                        {
                            // Extract character name from URL if needed to pre-fill the form
                            string characterName = HttpUtility.ParseQueryString(request.Url.Query).Get("name");
                            responseString = GenerateCharacterInfoHtml(characterName); // Method that generates HTML with character info in editable form
                        }
                        
                        
                        else
                        {
                            // Generate the initial page with dropdown
                            responseString = GenerateDropdownFromParties();
                        }
                    }

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    // Handle exceptions (log them or inform the user)
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
                finally
                {
                    response.OutputStream.Close();
                }
            }
        }


        public void StopServer()
        {
            if (listener != null)
            {
                isServerRunning = false;
                listener.Stop();
                listener.Close();
            }
        }
        public static void AddFirewallRule()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall add rule name=\"TTRPG-manager\" dir=in action=allow protocol=TCP localport=8080",
                Verb = "runas", // This attempts to run the process with administrator privileges
                CreateNoWindow = true,
                UseShellExecute = true,
            };

            try
            {
                using (Process exeProcess = Process.Start(startInfo))
                {
                    exeProcess.WaitForExit();
                }
            }
            catch
            {
                // Handle the case where the user did not allow administrator privileges
            }
        }

        public void AddFirewallRuleWithUserConsent()
        {
            // Prompt the user for consent before attempting to modify firewall settings
            var result = MessageBox.Show("The application needs to add a firewall rule to operate correctly. This requires administrative privileges. Proceed?",
                                         "Firewall Permission", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    AddFirewallRule(); // Call your method to add the firewall rule here
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add firewall rule: {ex.Message}");
                }
            }
        }
        public string GetLocalIPAddress()
        {
            string wirelessIP = null;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Check if the network interface is wireless and operational
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && ni.OperationalStatus == OperationalStatus.Up)
                {
                    var ipProps = ni.GetIPProperties();
                    // Get the first IPv4 address assigned to this interface
                    var ipInfo = ipProps.UnicastAddresses
                                        .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipInfo != null)
                    {
                        wirelessIP = ipInfo.Address.ToString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(wirelessIP))
            {
                throw new Exception("No operational wireless network interfaces with an IPv4 address found.");
            }

            return wirelessIP;
        }
        private string GenerateDropdownFromParties()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("<html><head><title>Select Character</title></head><body>");
            stringBuilder.Append("<meta name='viewport' content='width=device-width, initial-scale=1'>");
            stringBuilder.Append("<style>");
            stringBuilder.Append("body { font-size: 20px; }"); // Default font size for large screens
            stringBuilder.Append("p { font-size: 20px; }");
            stringBuilder.Append("select { font-size: 24px; }");
            stringBuilder.Append("input { font-size: 24px; }");
            stringBuilder.Append("@media (max-width: 1300px) { body { font-size: 32px; } }");
            stringBuilder.Append("@media (max-width: 1300px) { p { font-size: 24px; } }");// Larger font size for screens narrower than 600px
            stringBuilder.Append(".character-image { max-width: 100%; height: auto; position: absolute; top: 0; right: 0; }");
            stringBuilder.Append("@media (min-width: 100px) { .character-image { width: 40%; } }");
            stringBuilder.Append("@media (min-width: 1500px) { .character-image { width: 50%; } }");
            stringBuilder.Append("</style>");
            stringBuilder.Append("<h2>Choose your Character</h2>");
            stringBuilder.Append("<form method='GET' action='/editCharacter'>");
            stringBuilder.Append("<select name='name'>");

            
                foreach (var character in _config.Parties[_config.selectedPartyIndex].Members)
                {
                    stringBuilder.AppendFormat("<option value='{0}'>{0}</option>", character.Name);
                }
            

            stringBuilder.Append("</select>");
            stringBuilder.Append("<input type='submit' value='View Character'/>");
            stringBuilder.Append("</form>");
            stringBuilder.Append("</body></html>");

            return stringBuilder.ToString();
        }
        private string GenerateCharacterInfoHtml(string characterName)
        {
            var character = _config.Parties[_config.selectedPartyIndex].Members.FirstOrDefault(c => c.Name.Equals(characterName, StringComparison.OrdinalIgnoreCase));
            if (character == null) return "<html><body>Character not found.</body></html>";

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("<html><head><title>View Character</title></head><body>");
            stringBuilder.Append("<meta name='viewport' content='width=device-width, initial-scale=1'>");
            stringBuilder.Append("<style>");
            stringBuilder.Append("body { font-size: 20px; }"); // Default font size for large screens
            stringBuilder.Append("p { font-size: 20px; }");
            stringBuilder.Append("@media (max-width: 1300px) { body { font-size: 32px; } }");
            stringBuilder.Append("@media (max-width: 1300px) { p { font-size: 24px; } }");// Larger font size for screens narrower than 600px
            stringBuilder.Append(".character-image { max-width: 100%; height: auto; position: absolute; top: 0; right: 0; }");
            stringBuilder.Append("@media (min-width: 100px) { .character-image { width: 40%; } }");
            stringBuilder.Append("@media (min-width: 1500px) { .character-image { width: 50%; } }");
            stringBuilder.Append("</style>");
            stringBuilder.Append("</head><body>");
            stringBuilder.AppendFormat("<h2>{0}</h2><br>", character.Name);
            stringBuilder.Append("<form action='/updateCharacter' method='post'>");

            stringBuilder.AppendFormat("<img src='/images/{0}' class='character-image' alt='Character Image'/>", HttpUtility.UrlEncode(character.Name));

            // Add hidden input for character name to identify the character on submission
            stringBuilder.AppendFormat("<input type='hidden' name='Name' value='{0}'/>", character.Name);

            // For each editable property, create an appropriate input
            
            stringBuilder.AppendFormat("<textarea rows='5' cols='40' name='Description'>{0}</textarea></p>", character.Description);
            stringBuilder.AppendFormat("<p>Age: <input type='number' name='Age' value='{0}' /></p>", character.Age);
            stringBuilder.AppendFormat("<p>Gender: <input type='text' name='Gender' value='{0}' /></p>", character.Gender);
            stringBuilder.AppendFormat("<p>Race: <input type='text' name='Race' value='{0}' /></p>", character.Race);
            stringBuilder.AppendFormat("<p>Title: <input type='text' name='Title' value='{0}' /></p>", character.Title);

            // Inventory section
            stringBuilder.Append("<h3>Inventory</h3><ul>");
            foreach (var item in character.Inventory)
            {
                stringBuilder.AppendFormat("<li>{0} - {1} (Count: {2}, Uses: {3}/{4})</li>", item.Name, item.Description, item.Count, item.Uses, item.MaxUses);
            }
            stringBuilder.Append("</ul>");

            // Skills section
            stringBuilder.Append("<h3>Skills</h3><ul>");
            foreach (var skill in character.Skills)
            {
                stringBuilder.AppendFormat("<li>{0} - {1} (Damage: {2} {3}, MPCost: {4}, HPCost: {5}, Cooldown: {6}, Skill Level: {7}, Remaining Uses: {8}/{9})</li>", skill.Name, skill.Description, skill.DamageAmount, skill.DamageType, skill.MPCost, skill.HPCost, skill.Cooldown, skill.SkillLevel, skill.RemainingUses, skill.MaxUses);
            }
            stringBuilder.Append("</ul>");
            // Append more fields as needed, based on Character class properties

            stringBuilder.Append("<input type='submit' value='Update Character'/>");
            stringBuilder.Append("</form>");
            stringBuilder.Append("</body></html>");

            return stringBuilder.ToString();
        }

    }

}
