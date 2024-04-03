using System.Windows.Forms;
using Trans;

using static Trans.Parser;


namespace Trans2;

public partial class Form1 : Form
{
    private Dictionary<string, Control> _controls;


    public Form1()
    {
        InitializeComponent();
        comboBox1.SelectedItem = "Lexical analysis";

        richTextBox1.Multiline = true;
        richTextBox1.AcceptsTab = true;
        
        _controls = new()
        {
            ["Lexical analysis"] = dataGridView1,
            ["Syntax analysis"] = treeView1,
            ["Semantic analysis"] = treeView2
        };
    }

    private void richTextBox1_TextChanged(object sender, EventArgs e)
    {
        string text = richTextBox1.Text;

        IEnumerable<Token> tokens;

        try
        {
            tokens = Parser.LexicalAnalysis(text);

            UpdateTable(tokens.ToList());
        }
        catch (LexicalException ex)
        {
            SetTableError(ex);
            SetTreeError(ex);
            SetSemanticError(ex);
            return;
        }

        Node root;

        if (comboBox1.SelectedItem?.ToString() == "Syntax analysis" || comboBox1.SelectedItem?.ToString() == "Semantic analysis")
        {
            try
            {
                root = Parser.SyntaxAnalysis(tokens.ToList());

                UpdateTree(root);
            }
            catch (SyntaxException ex)
            {
                SetTreeError(ex);
                SetSemanticError(ex);

                return;
            }
        }
        else return;

        
        if (comboBox1.SelectedItem?.ToString() == "Semantic analysis")
        {
            try
            {
                Parser.SemanticAnalysis(root);

                UpdateSemanticTree(root);
            }
            catch (SemanticException ex)
            {
                SetSemanticError(ex);
            }
        }
    }

    private void UpdateTable(List<Token> tokens)
    {
        dataGridView1.Rows.Clear();

        if (tokens.Count > 0)
        {
            dataGridView1.Rows.Add(tokens.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                dataGridView1.Rows[i].Cells[0].Value = tokens[i].Id;
                dataGridView1.Rows[i].Cells[1].Value = tokens[i].TokenType;
                dataGridView1.Rows[i].Cells[2].Value = tokens[i].Value;
            }
        }
    }

    private void SetTableError(Exception ex)
    {
        dataGridView1.Rows.Clear();
        dataGridView1.Rows.Add(1);

        dataGridView1.Rows[0].Cells[0].Value = -1;
        dataGridView1.Rows[0].Cells[1].Value = "Error";
        dataGridView1.Rows[0].Cells[2].Value = ex.Message;
    }


    private void UpdateTree(Node root)
    {
        treeView1.Nodes.Clear();

        if (ToViewNode(root) is TreeNode treeNodeRoot)
            treeView1.Nodes.Add(treeNodeRoot);

        treeView1.ExpandAll();


        static TreeNode? ToViewNode(Node node)
        {
            if (node is EmptyNode)
            {
                return null;
            }
            if (node is ValueNode tokenNode)
            {
                TreeNode treeNode = new TreeNode(tokenNode.Token.Value);
                return treeNode;
            }
            else if (node is OperatorNode operatorNode)
            {
                TreeNode treeNode = new TreeNode(operatorNode.Operator);

                foreach(var child in operatorNode.Children)
                {
                    if (ToViewNode(child) is TreeNode treeNodeChild)
                        treeNode.Nodes.Add(treeNodeChild);
                }

                return treeNode;
            }
            else if (node is TypesNode typesNode)
            {
                return new TreeNode(string.Join(' ', typesNode.Types.Select(t => t.Value)));
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private void SetTreeError(Exception ex)
    {
        treeView1.Nodes.Clear();
        treeView1.Nodes.Add(ex.Message);
    }

    private void UpdateSemanticTree(Node root)
    {
        treeView2.Nodes.Clear();

        if (ToViewNode(root) is TreeNode treeNodeRoot)
            treeView2.Nodes.Add(treeNodeRoot);

        treeView2.ExpandAll();


        static TreeNode? ToViewNode(Node node)
        {
            if (node is EmptyNode)
            {
                return null;
            }
            if (node is ValueNode tokenNode)
            {
                TreeNode treeNode = new TreeNode(tokenNode.Token.Value);
                return treeNode;
            }
            else if (node is OperatorNode operatorNode)
            {
                if (operatorNode.Operator is "(..)" or "Line ;")
                {
                    return ToViewNode(operatorNode.Children.ToArray()[0]);
                }

                TreeNode treeNode = new TreeNode(operatorNode.Operator);

                foreach (var child in operatorNode.Children)
                {
                    if (ToViewNode(child) is TreeNode treeNodeChild)
                        treeNode.Nodes.Add(treeNodeChild);
                }

                return treeNode;
            }
            else if (node is TypesNode typesNode)
            {
                return new TreeNode(string.Join(' ', typesNode.Types.Select(t => t.Value)));
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private void SetSemanticError(Exception ex)
    {
        treeView2.Nodes.Clear();
        treeView2.Nodes.Add(ex.Message);
    }


    private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_controls?.TryGetValue(comboBox1.SelectedItem?.ToString()!, out Control? currentControl) ?? false)
        {
            foreach (Control control in _controls.Values)
            {
                control.Visible = false;
            }

            currentControl.Visible = true;

            richTextBox1_TextChanged(sender, e);
        }
    }
}
