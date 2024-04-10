using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class Task
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Comment { get; set; } = "";
    public bool IsCompleted { get; set; } = false;
    public int Level { get; set; } = 0;
    public List<Task> Children { get; } = new List<Task>();
    public void AddChild(Task child)
    {
        child.Level = Level + 1;
        Children.Add(child);
    }
    /// <summary>
    /// Converts the task to a Markdown string representation of its list item only.
    /// </summary>
    /// <returns>A Markdown string representing the task's list item.</returns>
    public string ToListMarkdownItem()
    {
        // Write the list item line
        var indent = new string(' ', Level * 2);
        var state = IsCompleted ? "X" : " ";
        return $"{indent}* [{state}] [link](#{Id}) {Name}";
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
            sb.AppendLine($"# {Id}");
            if (StartTime.HasValue)
            {
                sb.AppendLine("## start-time");
                sb.AppendLine($"{StartTime.Value.ToString("yyyy-MM-dd HH:mm")}");
            }
            if (EndTime.HasValue)
            {
                sb.AppendLine("## end-time");
                sb.AppendLine($"{EndTime.Value.ToString("yyyy-MM-dd HH:mm")}");
            }
            if (!string.IsNullOrEmpty(Comment))
            {
                sb.AppendLine("## comment");
                sb.AppendLine($"{Comment}");
            }
        }

        return sb.ToString().TrimEnd('\n'); // Trim the last newline
    }
}

public class TaskReader
{
    private const string TaskListItemPattern = @"^\s*\*\s*(\[(X| )\])\s*\[link\]\s*\((.*)\)\s*(.*)$";
    private const string TaskHeaderPattern = @"^#\s*(\w+)$";
    private const string DetailPattern = @"^##\s*(\w+)$";
    private Dictionary<string, Task> headersById = new Dictionary<string, Task>();
    /// <summary>
    /// Parses the given Markdown text and returns a list of tasks.
    /// </summary>
    /// <param name="markdownText">The Markdown text to parse.</param>
    /// <returns>A list of tasks extracted from the Markdown text.</returns>
    public List<Task> ParseMarkdown(string[] markdownText)
    {
        var SpanMD = markdownText.AsSpan();
        // Search for the start of the header domain
        var headerStartIndex = FindFirstHeaderIndex(SpanMD);

        if (headerStartIndex == -1)
        {
            throw new ArgumentException("Invalid Markdown format: Header domain not found at the interval of the file.");
        }


        CollectHeaderbyID(SpanMD[headerStartIndex..]);
        var rootTask = new Task()
        {
            //AddChild will use Level + 1
            Level = -1,
        };
        int start_index = 0;
        BuildTaskTree(SpanMD[..headerStartIndex], ref start_index, rootTask);
        return rootTask.Children;
    }
    private string CollectParagrahStr(Span<string> HeaderDomain, ref int search_index)
    {
        int start_index = search_index;
        for (; search_index < HeaderDomain.Length; search_index++)
        {
            if (HeaderDomain[search_index].StartsWith("#"))
            {
                break;
            }
        }
        Span<string> lines = HeaderDomain[start_index..search_index];
        return string.Join('\n', lines.ToArray());
    }
    private void CollectDetailsWithTask(Span<string> HeaderDomain, ref int search_index, Task task)
    {
        for (; search_index < HeaderDomain.Length; search_index++)
        {
            var line = HeaderDomain[search_index];
            var detailMatch = Regex.Match(line, DetailPattern);
            if (!detailMatch.Success)
            {
                return;
            }
            var detailKey = detailMatch.Groups[0].Value;
            var detailValue = CollectParagrahStr(HeaderDomain, ref search_index);
            // Update the current header's properties based on the detail key-value pair
            switch (detailKey)
            {
                case "name":
                    task.Name = detailValue;
                    break;
                case "start-time":
                    task.StartTime = DateTime.Parse(detailValue);
                    break;
                case "end-time":
                    task.EndTime = DateTime.Parse(detailValue);
                    break;
                case "comment":
                    task.Comment = detailValue;
                    break;
                default:
                    throw new ArgumentException($"Invalid detail key: {detailKey} in header: {task.Id}");
            }
        }
    }
    private void CollectHeaderbyID(Span<string> HeaderDomain)
    {
        // Collect all headers and their details from the remaining part of the file (after the list domain)
        var currentHeader = null as Task;
        for (int search_index = 0; search_index < HeaderDomain.Length;)
        {
            var line = HeaderDomain[search_index];
            var match = Regex.Match(line, TaskHeaderPattern);
            if (!match.Success)
            {
                throw new ArgumentException($"Invalid Markdown format: Header Parser Error at line: {line}");
            }
            var headerId = match.Groups[0].Value;
            var headerTask = new Task()
            {
                Id = headerId,
            };
            if (headersById.ContainsKey(headerId))
            {
                throw new ArgumentException($"Duplicate header ID: {headerId}");
            }
            headersById.Add(headerId, headerTask);
            search_index++;
            CollectDetailsWithTask(HeaderDomain, ref search_index, headerTask);
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ListDomain">Represents the entire list, containing solely list elements with no additional content.</param>
    /// <param name="start_index">The index at which scanning begins, shared throughout the recursive calls.</param>
    /// <param name="expect_level">Indicates the expected hierarchical level. The list is processed only when its level matches the <paramref name="expect_level"/>; 
    /// otherwise, it is directly returned.
    /// A special case occurs when <paramref name="expect_level"/> is -1, representing the very beginning and serving as a child node of the root task.</param>
    /// <param name="father">Specifies the parent node, i.e., the parent of the current node.</param>
    private void BuildTaskTree(Span<string> ListDomain, ref int start_index, Task father)
    {
        var line = ListDomain[start_index];
        start_index++;
        int level = CountLevelFromSpaces(line);
        if (level < father.Level + 1)
        {
            return;
        }
        if (level > father.Level + 1)
        {
            throw new Exception($"Invalid Task List Level : {line}");
        }
        var match = Regex.Match(line, TaskListItemPattern);
        var isCompleted = match.Groups[0].Value == "X";
        var id = match.Groups[1].Value;
        var name = match.Groups[2].Value;
        var task = new Task()
        {
            Id = id,
            Name = name,
            IsCompleted = isCompleted
        };
        father.AddChild(task);
        BuildTaskTree(ListDomain, ref start_index, task);
    }
    /// <summary>
    /// Finds the starting position of task details within the given markdown text.
    /// </summary>
    /// <param name="markdownText">An array of strings containing the markdown text.</param>
    /// <returns>The index of the starting line for task details if found, otherwise -1.</returns>
    private int FindFirstHeaderIndex(Span<string> markdownText)
    {
        var lines = markdownText;
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], TaskHeaderPattern))
            {
                return i;
            }
        }
        return -1;
    }
    private int CountLevelFromSpaces(string line)
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
        return count / 4;
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
