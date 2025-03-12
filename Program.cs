using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

var app = WebApplication.Create();

// Serve static files (HTML, CSS, JS)
app.UseStaticFiles();

string messagesFile = "messages.json"; // Store messages in a JSON file
string commentsFile = "comments.json"; // Store comments per post
string usersFile = "users.json"; // Store users for login/signup

// Ensure JSON files exist
if (!File.Exists(messagesFile)) File.WriteAllText(messagesFile, "[]");
if (!File.Exists(commentsFile)) File.WriteAllText(commentsFile, "{}");
if (!File.Exists(usersFile)) File.WriteAllText(usersFile, "[]");

// Route to get all messages
app.MapGet("/messages", async context =>
{
    var messages = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(await File.ReadAllTextAsync(messagesFile))!;

    // Ensure each message has a unique ID
    foreach (var msg in messages)
    {
        if (!msg.ContainsKey("id"))
            msg["id"] = Guid.NewGuid().ToString();
    }

    await File.WriteAllTextAsync(messagesFile, JsonSerializer.Serialize(messages));

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(JsonSerializer.Serialize(messages));
});

// Route to submit a new message
app.MapPost("/post", async context =>
{
    var form = await context.Request.ReadFormAsync();
    string name = form["name"];
    string message = form["message"];

    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(message))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Name and message cannot be empty.");
        return;
    }

    var messages = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(await File.ReadAllTextAsync(messagesFile))!;
    var newMessage = new Dictionary<string, string>
    {
        { "id", Guid.NewGuid().ToString() },
        { "name", name },
        { "message", message },
        { "timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
    };

    messages.Insert(0, newMessage);
    await File.WriteAllTextAsync(messagesFile, JsonSerializer.Serialize(messages));

    context.Response.Redirect("/bulletinboard");
});

// Route to fetch comments for a specific post
app.MapGet("/comments", async context =>
{
    var query = context.Request.Query;
    if (!query.ContainsKey("postId"))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Missing postId parameter.");
        return;
    }

    string postId = query["postId"].ToString();
    var commentsData = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(await File.ReadAllTextAsync(commentsFile))!;

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(JsonSerializer.Serialize(commentsData.ContainsKey(postId) ? commentsData[postId] : new List<Dictionary<string, string>>()));
});

// Route to submit a comment
app.MapPost("/add-comment", async context =>
{
    try
    {
        // Read the request body
        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var comment = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

        // Ensure the request contains required fields
        if (comment == null || !comment.ContainsKey("messageId") || !comment.ContainsKey("user") || !comment.ContainsKey("text"))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid comment data. Ensure messageId, user, and text are provided.");
            return;
        }

        string messageId = comment["messageId"]; // Use correct key name
        comment["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Load existing comments
        var commentsData = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(await File.ReadAllTextAsync(commentsFile)) ?? new Dictionary<string, List<Dictionary<string, string>>>();

        // Ensure there's a list for this message ID
        if (!commentsData.ContainsKey(messageId))
            commentsData[messageId] = new List<Dictionary<string, string>>();

        // Add the new comment
        commentsData[messageId].Add(comment);

        // Write back to file
        await File.WriteAllTextAsync(commentsFile, JsonSerializer.Serialize(commentsData));

        // Return success response
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("Comment submitted successfully.");
    }
    catch (Exception ex)
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsync($"Failed to submit comment: {ex}");
}

});


// User authentication: Fetch all users
app.MapGet("/users", async context =>
{
    var users = await File.ReadAllTextAsync(usersFile);
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(users);
});

// Endpoint to handle user login
app.MapPost("/login", async context =>
{
    var form = await context.Request.ReadFormAsync();
    string username = form["username"];
    string password = form["password"];

    var users = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(await File.ReadAllTextAsync(usersFile))!;

    var user = users.Find(u => u["username"] == username && u["password"] == password);

    if (user != null)
    {
        context.Response.Cookies.Append("loggedInUser", username, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });
        await context.Response.WriteAsync("Success");
    }
    else
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid credentials");
    }
});

// Endpoint for user signup
app.MapPost("/signup", async context =>
{
    var form = await context.Request.ReadFormAsync();
    string username = form["username"];
    string password = form["password"];

    var users = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(await File.ReadAllTextAsync(usersFile))!;

    if (users.Any(user => user["username"] == username))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Username already exists. Please choose another one.");
        return;
    }

    users.Add(new Dictionary<string, string>
    {
        { "username", username },
        { "password", password }
    });

    await File.WriteAllTextAsync(usersFile, JsonSerializer.Serialize(users));

    context.Response.Redirect("/login.html");
});

// Route to serve the bulletin board page
app.MapGet("/bulletinboard", async context =>
{
    await context.Response.SendFileAsync("wwwroot/index.html");
});

// Start the backend server
app.Run("http://0.0.0.0:5077");
