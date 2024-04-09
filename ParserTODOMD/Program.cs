using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class Task
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Comment { get; set; }
    public bool IsCompleted { get; set; }
    public List<Task> Children { get; } = new List<Task>();

    public Task(string id, bool isCompleted)
    {
        Id = id;
        IsCompleted = isCompleted;
        Name = "";
    }
        /// <summary>
    /// Converts the task to a Markdown string representation of its list item only.
    /// </summary>
    /// <returns>A Markdown string representing the task's list item.</returns>
    public string ToListMarkdownItem()
    {
        var sb = new StringBuilder();

        // Write the list item line
        var state = IsCompleted ? "X" : " ";
        sb.Append($"   {{[{state}]}} [link] (#{Id})");
        
        if (!string.IsNullOrEmpty(Name))
        {
            sb.AppendLine($" ({Name})");
        }
        else
        {
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts the task's details to a Markdown string representation.
    /// </summary>
    /// <returns>A Markdown string representing the task's details.</returns>
    public string ToDetailsMarkdown()
    {
        var sb = new StringBuilder();

        // Write the task's details (if any)
        if (StartTime.HasValue || EndTime.HasValue || !string.IsNullOrEmpty(Comment))
        {
            sb.AppendLine($"   # Details:");
            if (StartTime.HasValue)
            {
                sb.AppendLine($"      - Start Time: {StartTime.Value.ToString("yyyy-MM-dd HH:mm")}");
            }
            if (EndTime.HasValue)
            {
                sb.AppendLine($"      - End Time: {EndTime.Value.ToString("yyyy-MM-dd HH:mm")}");
            }
            if (!string.IsNullOrEmpty(Comment))
            {
                sb.AppendLine($"      - Comment: {Comment}");
            }
        }

        return sb.ToString().TrimEnd('\n'); // Trim the last newline
    }
}

public class TaskReader
{
    private const string TaskListItemPattern = @"^\s*(\[(X| )\])\s*(\[link\])\s*(.*)$";
    private const string TaskHeaderPattern = @"^# (\w+)$";
    private const string DetailPattern = @"^## (\w+):\s*(.*)$";

    /// <summary>
    /// Parses the given Markdown text and returns a list of tasks.
    /// </summary>
    /// <param name="markdownText">The Markdown text to parse.</param>
    /// <returns>A list of tasks extracted from the Markdown text.</returns>
    public List<Task> ParseMarkdown(string markdownText)
    {
        var rootTasks = new List<Task>();
        var headersById = new Dictionary<string, Task>();
        var currentIndent = 0;
        var currentParent = null as Task;
        var currentLine = 0;

        // Search for the start of the list domain
        var listStartIndex = FindListStart(markdownText);

        if (listStartIndex == -1)
        {
            throw new ArgumentException("Invalid Markdown format: List domain not found at the beginning of the file.");
        }

        // Collect all headers and their details from the remaining part of the file (after the list domain)
        var postListMarkdown = markdownText.Substring(listStartIndex);
        var currentHeader = null as Task;
        foreach (var line in postListMarkdown.Split('\n'))
        {
            if (Regex.IsMatch(line, TaskHeaderPattern))
            {
                var match = Regex.Match(line, TaskHeaderPattern);
                var headerId = match.Groups[1].Value;
                var headerTask = new Task(headerId, false);
                headersById.Add(headerId, headerTask);
                currentHeader = headerTask;
            }
            else if (currentHeader != null && Regex.IsMatch(line, DetailPattern))
            {
                var detailMatch = Regex.Match(line, DetailPattern);
                var detailKey = detailMatch.Groups[1].Value.ToLower();
                var detailValue = detailMatch.Groups[2].Value;

                // Update the current header's properties based on the detail key-value pair
                switch (detailKey)
                {
                    case "name":
                        currentHeader.Name = detailValue;
                        break;
                    case "start-time":
                        currentHeader.StartTime = DateTime.Parse(detailValue);
                        break;
                    case "end-time":
                        currentHeader.EndTime = DateTime.Parse(detailValue);
                        break;
                    case "comment":
                        currentHeader.Comment = detailValue;
                        break;
                }
            }
        }

        // Parse the list items, associating them with their corresponding headers
        var listMarkdown = markdownText.Substring(0, listStartIndex);
        foreach (var line in listMarkdown.Split('\n'))
        {
            currentLine++;

            if (Regex.IsMatch(line, TaskListItemPattern))
            {
                var match = Regex.Match(line, TaskListItemPattern);
                var isCompleted = match.Groups[1].Value == "[X]";
                var id = match.Groups[3].Value;
                var task = new Task(id, isCompleted);

                var indent = CountLeadingSpaces(line);
                if (indent <= currentIndent)
                {
                    while (indent < currentIndent)
                    {
                        currentParent ??= rootTasks.LastOrDefault();
                        currentIndent--;
                    }
                }
                else
                {
                    currentIndent = indent;
                }

                if (currentParent != null)
                {
                    currentParent.Children.Add(task);
                }
                else
                {
                    rootTasks.Add(task);
                }

                // Try to associate the task with its corresponding header
                if (headersById.TryGetValue(id, out var headerTask))
                {
                    task.Name = headerTask.Name;
                    task.StartTime = headerTask.StartTime;
                    task.EndTime = headerTask.EndTime;
                    task.Comment = headerTask.Comment;
                }

                currentParent = task;
            }
        }

        return rootTasks;
    }

    /// <summary>
    /// Finds the index of the first line containing a list item pattern in the given Markdown text.
    /// Returns -1 if no list item is found.
    /// </summary>
    /// <param name="markdownText">The Markdown text to search in.</param>
    /// <returns>The index of the first list item line or -1 if not found.</returns>
    private int FindListStart(string markdownText)
    {
        var lines = markdownText.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], TaskListItemPattern))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Counts the number of leading spaces in the given line.
    /// </summary>
    /// <param name="line">The line to count leading spaces in.</param>
    /// <returns>The number of leading spaces in the line.</returns>
    private int CountLeadingSpaces(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return count;
    }
}
public class TaskWriter
{
    // ... (existing TaskParser class code)

    /// <summary>
    /// Writes the given list of tasks to a Markdown file.
    /// </summary>
    /// <param name="tasks">The list of tasks to write.</param>
    /// <param name="filePath">The path of the output Markdown file.</param>
    public void WriteToMarkdownFile(List<Task> tasks, string filePath)
    {
        using (var writer = new StreamWriter(filePath))
        {
            // First pass: Write only list items (recursively)
            foreach (var task in tasks)
            {
                WriteTaskListMarkdown(writer, task);
            }

            // Second pass: Write details for each task (recursively)
            foreach (var task in tasks)
            {
                WriteTaskDetailsMarkdown(writer, task);
            }
        }
    }

    private void WriteTaskListMarkdown(StreamWriter writer, Task task)
    {
        writer.Write(task.ToListMarkdownItem());
        writer.WriteLine(); // Add an extra newline between tasks

        foreach (var child in task.Children)
        {
            WriteTaskListMarkdown(writer, child);
        }
    }

    private void WriteTaskDetailsMarkdown(StreamWriter writer, Task task)
    {
        writer.Write(task.ToDetailsMarkdown());
        writer.WriteLine(); // Add an extra newline between tasks

        foreach (var child in task.Children)
        {
            WriteTaskDetailsMarkdown(writer, child);
        }
    }
}
