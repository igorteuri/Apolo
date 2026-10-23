using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// (the namespace is the one of the window program, where these classes were born)
namespace Ares.Player
{
    /// <summary>A folder ("mode" of the project) of the folder tree; it can contain other folders.</summary>
    public class ModeNode
    {
        public string Title;
        public bool Collapsed;
        public ModeNode Parent;
        public List<ModeNode> Children = new List<ModeNode>();

        public int Depth
        {
            get
            {
                int d = 0;
                for (ModeNode n = Parent; n != null && n.Title != null; n = n.Parent)
                    d++;
                return d;
            }
        }

        public bool IsDescendantOf(ModeNode other)
        {
            for (ModeNode n = Parent; n != null; n = n.Parent)
            {
                if (n == other)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// How the modes of a project are nested in the list (folders inside folders). The project file of Ares
    /// keeps the modes flat, so the nesting is stored beside the project in "[project].pastas.json".
    /// </summary>
    public class ModeTree
    {
        public readonly ModeNode Root = new ModeNode();

        public static string FileFor(string projectPath)
        {
            return projectPath + ".pastas.json";
        }

        public static ModeTree Load(string projectPath)
        {
            ModeTree tree = new ModeTree();
            try
            {
                if (string.IsNullOrEmpty(projectPath) || !File.Exists(FileFor(projectPath)))
                    return tree;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FileFor(projectPath), Encoding.UTF8)))
                {
                    JsonElement nodes;
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("nodes", out nodes))
                        ReadNodes(nodes, tree.Root);
                }
            }
            catch (Exception)
            {
                // an unreadable file just means "no folders yet"
            }
            return tree;
        }

        private static void ReadNodes(JsonElement list, ModeNode parent)
        {
            if (list.ValueKind != JsonValueKind.Array)
                return;
            foreach (JsonElement item in list.EnumerateArray())
            {
                JsonElement title;
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("title", out title) || title.ValueKind != JsonValueKind.String)
                    continue;
                ModeNode node = new ModeNode();
                node.Title = title.GetString();
                JsonElement collapsed;
                node.Collapsed = item.TryGetProperty("collapsed", out collapsed) && collapsed.ValueKind == JsonValueKind.True;
                node.Parent = parent;
                parent.Children.Add(node);
                JsonElement children;
                if (item.TryGetProperty("children", out children))
                    ReadNodes(children, node);
            }
        }

        private static void WriteNodes(Utf8JsonWriter w, ModeNode parent)
        {
            w.WriteStartArray();
            foreach (ModeNode n in parent.Children)
            {
                w.WriteStartObject();
                w.WriteString("title", n.Title);
                if (n.Collapsed)
                    w.WriteBoolean("collapsed", true);
                if (n.Children.Count > 0)
                {
                    w.WritePropertyName("children");
                    WriteNodes(w, n);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        public void Save(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            try
            {
                string file = FileFor(projectPath);
                bool nested = Root.Children.Any(n => n.Children.Count > 0);
                if (!nested && !File.Exists(file))
                    return; // nothing to remember yet
                using (MemoryStream stream = new MemoryStream())
                {
                    using (Utf8JsonWriter w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                    {
                        w.WriteStartObject();
                        w.WriteNumber("version", 1);
                        w.WritePropertyName("nodes");
                        WriteNodes(w, Root);
                        w.WriteEndObject();
                    }
                    File.WriteAllBytes(file, stream.ToArray());
                }
            }
            catch (Exception)
            {
            }
        }

        public IEnumerable<ModeNode> All()
        {
            Stack<ModeNode> stack = new Stack<ModeNode>();
            for (int i = Root.Children.Count - 1; i >= 0; i--)
                stack.Push(Root.Children[i]);
            while (stack.Count > 0)
            {
                ModeNode n = stack.Pop();
                yield return n;
                for (int i = n.Children.Count - 1; i >= 0; i--)
                    stack.Push(n.Children[i]);
            }
        }

        public ModeNode Find(string title)
        {
            if (title == null)
                return null;
            foreach (ModeNode n in All())
            {
                if (string.Equals(n.Title, title, StringComparison.Ordinal))
                    return n;
            }
            return null;
        }

        /// <summary>Makes the tree match the modes of the project: new modes go to the end of the top level,
        /// vanished ones disappear (their sub folders move up). Returns true if something changed.</summary>
        public bool Sync(IList<string> titles)
        {
            bool changed = false;
            HashSet<string> wanted = new HashSet<string>(titles, StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModeNode n in All().ToList())
            {
                if (wanted.Contains(n.Title) && seen.Add(n.Title))
                    continue;
                int idx = n.Parent.Children.IndexOf(n);
                n.Parent.Children.RemoveAt(idx);
                foreach (ModeNode c in n.Children)
                    c.Parent = n.Parent;
                n.Parent.Children.InsertRange(idx, n.Children);
                n.Children = new List<ModeNode>();
                changed = true;
            }
            foreach (string title in titles)
            {
                if (seen.Add(title))
                {
                    Root.Children.Add(new ModeNode { Title = title, Parent = Root });
                    changed = true;
                }
            }
            return changed;
        }

        public void Detach(ModeNode node)
        {
            if (node.Parent != null)
                node.Parent.Children.Remove(node);
            node.Parent = null;
        }

        public void Attach(ModeNode node, ModeNode parent, int index)
        {
            Detach(node);
            node.Parent = parent;
            if (index < 0 || index > parent.Children.Count)
                parent.Children.Add(node);
            else
                parent.Children.Insert(index, node);
        }
    }
}
