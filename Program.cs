using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Template Method - базовий клас з lifecycle hooks
abstract class LightNodeWithLifecycle : LightNode
{
    // Template method
    public string Render()
    {
        OnBeforeRender();
        string result = OuterHTML;
        OnAfterRender();
        return result;
    }

    // Hooks (можуть бути перевизначені)
    protected virtual void OnBeforeRender() { }
    protected virtual void OnAfterRender() { }
    protected virtual void OnCreated() { }
    protected virtual void OnInserted() { }
    protected virtual void OnRemoved() { }
    protected virtual void OnStylesApplied() { }
    protected virtual void OnClassListApplied() { }
    protected virtual void OnTextRendered() { }

    public LightNodeWithLifecycle()
    {
        OnCreated();
        Console.WriteLine($"[LIFECYCLE] Element created: {GetType().Name}");
    }

    public void Insert()
    {
        OnInserted();
        Console.WriteLine($"[LIFECYCLE] Element inserted: {GetType().Name}");
    }

    public void Remove()
    {
        OnRemoved();
        Console.WriteLine($"[LIFECYCLE] Element removed: {GetType().Name}");
    }
}

// Оновлений LightElementNode з lifecycle
class LightElementNodeWithHooks : LightNodeWithLifecycle
{
    private string _tagName;
    private List<string> _cssClasses = new List<string>();
    private List<LightNode> _children = new List<LightNode>();

    public LightElementNodeWithHooks(string tagName)
    {
        _tagName = tagName;
    }

    public void AddClass(string cssClass)
    {
        _cssClasses.Add(cssClass);
        OnClassListApplied();
        Console.WriteLine($"[LIFECYCLE] Class '{cssClass}' applied to <{_tagName}>");
    }

    public void SetText(string text)
    {
        _children.Clear();
        _children.Add(new LightTextNode(text));
        OnTextRendered();
        Console.WriteLine($"[LIFECYCLE] Text rendered in <{_tagName}>: '{text}'");
    }

    public void AddChild(LightNode node)
    {
        _children.Add(node);
    }

    public override string InnerHTML => string.Join("", _children.Select(c => c.OuterHTML));

    public override string OuterHTML
    {
        get
        {
            var classAttr = _cssClasses.Count > 0 ? $" class=\"{string.Join(" ", _cssClasses)}\"" : "";
            return $"<{_tagName}{classAttr}>{InnerHTML}</{_tagName}>";
        }
    }
}

// Інтерфейс ітератора
interface IIterator<T>
{
    bool HasNext();
    T Next();
    void Reset();
}

// Конкретні ітератори
class DepthFirstIterator : IIterator<LightNode>
{
    private Stack<LightNode> _stack = new Stack<LightNode>();
    private LightNode _root;

    public DepthFirstIterator(LightNode root)
    {
        _root = root;
        Reset();
    }

    public bool HasNext() => _stack.Count > 0;

    public LightNode Next()
    {
        if (!HasNext()) return null;
        var current = _stack.Pop();

        // Додаємо дітей у зворотньому порядку для правильного DFS
        if (current is LightElementNode element)
        {
            var children = element.GetType().GetField("_children",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(element) as List<LightNode>;

            if (children != null)
            {
                for (int i = children.Count - 1; i >= 0; i--)
                    _stack.Push(children[i]);
            }
        }

        return current;
    }

    public void Reset()
    {
        _stack.Clear();
        _stack.Push(_root);
    }
}

class BreadthFirstIterator : IIterator<LightNode>
{
    private Queue<LightNode> _queue = new Queue<LightNode>();
    private LightNode _root;

    public BreadthFirstIterator(LightNode root)
    {
        _root = root;
        Reset();
    }

    public bool HasNext() => _queue.Count > 0;

    public LightNode Next()
    {
        if (!HasNext()) return null;
        var current = _queue.Dequeue();

        if (current is LightElementNode element)
        {
            var children = element.GetType().GetField("_children",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(element) as List<LightNode>;

            if (children != null)
            {
                foreach (var child in children)
                    _queue.Enqueue(child);
            }
        }

        return current;
    }

    public void Reset()
    {
        _queue.Clear();
        _queue.Enqueue(_root);
    }
}

// Агрегат (колекція)
interface IHTMLDocument
{
    IIterator<LightNode> CreateDepthFirstIterator();
    IIterator<LightNode> CreateBreadthFirstIterator();
}

class HTMLDocument : IHTMLDocument
{
    private LightNode _root;

    public HTMLDocument(LightNode root)
    {
        _root = root;
    }

    public IIterator<LightNode> CreateDepthFirstIterator()
    {
        return new DepthFirstIterator(_root);
    }

    public IIterator<LightNode> CreateBreadthFirstIterator()
    {
        return new BreadthFirstIterator(_root);
    }
}

// Інтерфейс відвідувача
interface IVisitor
{
    void Visit(LightTextNode textNode);
    void Visit(LightElementNode elementNode);
}

// Елементи, які можна відвідати
interface IVisitable
{
    void Accept(IVisitor visitor);
}

// Модифікуємо існуючі класи
class LightTextNodeVisitable : LightTextNode, IVisitable
{
    public LightTextNodeVisitable(string text) : base(text) { }

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
    }
}

class LightElementNodeVisitable : LightElementNode, IVisitable
{
    public LightElementNodeVisitable(string tagName, string displayType = "block", bool isSelfClosing = false)
        : base(tagName, displayType, isSelfClosing) { }

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        // Відвідуємо дітей
        var childrenField = this.GetType().BaseType.GetField("_children",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (childrenField?.GetValue(this) is List<LightNode> children)
        {
            foreach (var child in children.OfType<IVisitable>())
            {
                child.Accept(visitor);
            }
        }
    }
}

// Конкретні відвідувачі
class HTMLValidator : IVisitor
{
    private List<string> _errors = new List<string>();
    private List<string> _warnings = new List<string>();

    public void Visit(LightTextNode textNode)
    {
        // Перевіряємо, чи текст не порожній
        if (string.IsNullOrWhiteSpace(textNode.OuterHTML))
        {
            _warnings.Add("Empty text node found");
        }
    }

    public void Visit(LightElementNode elementNode)
    {
        // Перевіряємо теги
        var tagName = elementNode.GetType().GetField("_tagName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(elementNode) as string;

        if (tagName == "script")
        {
            _warnings.Add("Script tag detected - potential security risk");
        }

        if (tagName == "div" && elementNode.OuterHTML.Length > 1000)
        {
            _errors.Add($"Div element too large: {elementNode.OuterHTML.Length} chars");
        }
    }

    public void PrintReport()
    {
        Console.WriteLine("\nValidation Report ");
        Console.WriteLine($"Errors: {_errors.Count}");
        foreach (var err in _errors) Console.WriteLine($"  ERROR: {err}");
        Console.WriteLine($"Warnings: {_warnings.Count}");
        foreach (var warn in _warnings) Console.WriteLine($"  WARNING: {warn}");
    }
}

class ClassCounter : IVisitor
{
    private Dictionary<string, int> _classUsage = new Dictionary<string, int>();

    public void Visit(LightTextNode textNode) { }

    public void Visit(LightElementNode elementNode)
    {
        // Отримуємо класи (спрощено)
        var classesField = elementNode.GetType().GetField("_cssClasses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (classesField?.GetValue(elementNode) is List<string> classes)
        {
            foreach (var className in classes)
            {
                if (_classUsage.ContainsKey(className))
                    _classUsage[className]++;
                else
                    _classUsage[className] = 1;
            }
        }
    }

    public void PrintStats()
    {
        Console.WriteLine("\n CSS Class Statistics ");
        foreach (var kvp in _classUsage)
        {
            Console.WriteLine($"  .{kvp.Key}: {kvp.Value} time(s)");
        }
    }
}

// Клас для кольорового виведення в консоль
class Logger
{
    public void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[LOG] {message}");
        Console.ResetColor();
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
    }
}

// Клас для запису у файл
class FileWriter
{
    public void Write(string text)
    {
        File.AppendAllText("log.txt", text);
    }

    public void WriteLine(string text)
    {
        File.AppendAllText("log.txt", text + Environment.NewLine);
    }
}

// Інтерфейс, який очікує клієнт
interface ILogger
{
    void Log(string message);
    void Error(string message);
    void Warn(string message);
}

// Адаптер для FileWriter
class FileLoggerAdapter : ILogger
{
    private FileWriter _fileWriter;

    public FileLoggerAdapter(FileWriter fileWriter)
    {
        _fileWriter = fileWriter;
    }

    public void Log(string message)
    {
        _fileWriter.WriteLine($"[LOG] {message}");
    }

    public void Error(string message)
    {
        _fileWriter.WriteLine($"[ERROR] {message}");
    }

    public void Warn(string message)
    {
        _fileWriter.WriteLine($"[WARN] {message}");
    }
}

// Базовий клас героя
abstract class Hero
{
    public string Name { get; set; }
    public abstract string GetDescription();
}

// Конкретні герої
class Warrior : Hero
{
    public Warrior() => Name = "Warrior";
    public override string GetDescription() => Name;
}

class Mage : Hero
{
    public Mage() => Name = "Mage";
    public override string GetDescription() => Name;
}

class Paladin : Hero
{
    public Paladin() => Name = "Paladin";
    public override string GetDescription() => Name;
}

// Базовий декоратор
abstract class InventoryDecorator : Hero
{
    protected Hero _hero;
    public InventoryDecorator(Hero hero) => _hero = hero;
    public override string GetDescription() => _hero.GetDescription();
}

// Конкретні декоратори
class Armor : InventoryDecorator
{
    public Armor(Hero hero) : base(hero) { }
    public override string GetDescription() => _hero.GetDescription() + ", with Armor";
}

class Weapon : InventoryDecorator
{
    public Weapon(Hero hero) : base(hero) { }
    public override string GetDescription() => _hero.GetDescription() + ", with Weapon";
}

class Artifact : InventoryDecorator
{
    public Artifact(Hero hero) : base(hero) { }
    public override string GetDescription() => _hero.GetDescription() + ", with Artifact";
}

// Інтерфейс рендерера
interface IRenderer
{
    void Render(string shapeName);
}

// Конкретні рендерери
class VectorRenderer : IRenderer
{
    public void Render(string shapeName)
    {
        Console.WriteLine($"Drawing {shapeName} as vectors");
    }
}

class RasterRenderer : IRenderer
{
    public void Render(string shapeName)
    {
        Console.WriteLine($"Drawing {shapeName} as pixels");
    }
}

// Базовий клас фігури
abstract class Shape
{
    protected IRenderer _renderer;
    protected Shape(IRenderer renderer) => _renderer = renderer;
    public abstract void Draw();
}

// Конкретні фігури
class Circle : Shape
{
    public Circle(IRenderer renderer) : base(renderer) { }
    public override void Draw() => _renderer.Render("Circle");
}

class Square : Shape
{
    public Square(IRenderer renderer) : base(renderer) { }
    public override void Draw() => _renderer.Render("Square");
}

class Triangle : Shape
{
    public Triangle(IRenderer renderer) : base(renderer) { }
    public override void Draw() => _renderer.Render("Triangle");
}

// Інтерфейс для читання тексту
interface ITextReader
{
    char[][] ReadFile(string path);
}

// Реальний об'єкт
class SmartTextReader : ITextReader
{
    public char[][] ReadFile(string path)
    {
        var lines = File.ReadAllLines(path);
        char[][] result = new char[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
            result[i] = lines[i].ToCharArray();
        return result;
    }
}

// Проксі з логуванням
class SmartTextChecker : ITextReader
{
    private SmartTextReader _reader = new SmartTextReader();

    public char[][] ReadFile(string path)
    {
        Console.WriteLine($"[LOG] Opening file: {path}");
        char[][] data = _reader.ReadFile(path);
        Console.WriteLine($"[LOG] File read. Lines: {data.Length}, Total chars: {data.Sum(line => line.Length)}");
        Console.WriteLine($"[LOG] Closing file.");
        return data;
    }
}

// Проксі з обмеженням доступу
class SmartTextReaderLocker : ITextReader
{
    private SmartTextReader _reader = new SmartTextReader();
    private Regex _regex;

    public SmartTextReaderLocker(string pattern)
    {
        _regex = new Regex(pattern);
    }

    public char[][] ReadFile(string path)
    {
        if (_regex.IsMatch(path))
        {
            Console.WriteLine("Access denied!");
            return null;
        }
        return _reader.ReadFile(path);
    }
}

// Базовий клас для всіх вузлів HTML
abstract class LightNode
{
    public abstract string OuterHTML { get; }
    public abstract string InnerHTML { get; }
}

// Текстовий вузол
class LightTextNode : LightNode
{
    private string _text;
    public LightTextNode(string text) => _text = text;

    public override string InnerHTML => _text;
    public override string OuterHTML => _text;
}

// Елемент HTML
class LightElementNode : LightNode
{
    private string _tagName;
    private string _displayType;
    private bool _isSelfClosing;
    private List<string> _cssClasses = new List<string>();
    private List<LightNode> _children = new List<LightNode>();

    public LightElementNode(string tagName, string displayType = "block", bool isSelfClosing = false)
    {
        _tagName = tagName;
        _displayType = displayType;
        _isSelfClosing = isSelfClosing;
    }

    public void AddClass(string cssClass) => _cssClasses.Add(cssClass);
    public void AddChild(LightNode node) => _children.Add(node);

    public override string InnerHTML
    {
        get
        {
            if (_isSelfClosing) return "";
            return string.Join("", _children.Select(c => c.OuterHTML));
        }
    }

    public override string OuterHTML
    {
        get
        {
            var classAttr = _cssClasses.Count > 0 ? $" class=\"{string.Join(" ", _cssClasses)}\"" : "";
            if (_isSelfClosing)
                return $"<{_tagName}{classAttr} />";

            return $"<{_tagName}{classAttr}>{InnerHTML}</{_tagName}>";
        }
    }
}

// Легковаговик спільна частина HTML елемента
class LightElementNodeFlyweight
{
    private string _tagName;
    private string _displayType;
    private bool _isSelfClosing;
    private List<string> _cssClasses;

    public LightElementNodeFlyweight(string tagName, string displayType = "block", bool isSelfClosing = false, List<string> cssClasses = null)
    {
        _tagName = tagName;
        _displayType = displayType;
        _isSelfClosing = isSelfClosing;
        _cssClasses = cssClasses ?? new List<string>();
    }

    public string GetOuterHTML(string innerHTML)
    {
        var classAttr = _cssClasses.Count > 0 ? $" class=\"{string.Join(" ", _cssClasses)}\"" : "";
        if (_isSelfClosing)
            return $"<{_tagName}{classAttr} />";
        return $"<{_tagName}{classAttr}>{innerHTML}</{_tagName}>";
    }
}

// Вузол, який використовує легковаговик
class LightElementNodeWithFlyweight : LightNode
{
    private LightElementNodeFlyweight _flyweight;
    private string _innerHTML;

    public LightElementNodeWithFlyweight(LightElementNodeFlyweight flyweight, string innerHTML = "")
    {
        _flyweight = flyweight;
        _innerHTML = innerHTML;
    }

    public override string InnerHTML => _innerHTML;
    public override string OuterHTML => _flyweight.GetOuterHTML(_innerHTML);
}

class Program
{

    static void Main()
    {

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("\nTemplate Method Demo");
        var divWithHooks = new LightElementNodeWithHooks("div");
        divWithHooks.AddClass("container");
        divWithHooks.SetText("Hello World!");
        divWithHooks.Insert();
        Console.WriteLine($"Rendered: {divWithHooks.Render()}");
        divWithHooks.Remove();


        Console.WriteLine("\n Iterator Demo ");
        var rootDiv = new LightElementNode("html");
        var body = new LightElementNode("body");
        body.AddChild(new LightTextNode("Text 1"));
        var p = new LightElementNode("p");
        p.AddChild(new LightTextNode("Paragraph text"));
        body.AddChild(p);
        body.AddChild(new LightTextNode("Text 2"));
        rootDiv.AddChild(body);

        var doc = new HTMLDocument(rootDiv);

        Console.WriteLine("Depth-First Traversal:");
        var dfsIterator = doc.CreateDepthFirstIterator();
        while (dfsIterator.HasNext())
        {
            var node = dfsIterator.Next();
            Console.WriteLine($"  {node.GetType().Name}: {node.OuterHTML}");
        }

        Console.WriteLine("\nBreadth-First Traversal:");
        var bfsIterator = doc.CreateBreadthFirstIterator();
        while (bfsIterator.HasNext())
        {
            var node = bfsIterator.Next();
            Console.WriteLine($"  {node.GetType().Name}: {node.OuterHTML}");
        }

        Console.WriteLine("\n Visitor Demo ");
        // Створюємо HTML структуру
        var htmlDoc = new LightElementNodeVisitable("html");
        var bodyVis = new LightElementNodeVisitable("body");
        bodyVis.AddClass("main");
        bodyVis.AddClass("content");

        var divVis = new LightElementNodeVisitable("div");
        divVis.AddClass("container");
        divVis.AddChild(new LightTextNodeVisitable("Hello World"));

        bodyVis.AddChild(divVis);
        htmlDoc.AddChild(bodyVis);

        // Відвідувачі
        var validator = new HTMLValidator();
        var counter = new ClassCounter();

        htmlDoc.Accept(validator);
        htmlDoc.Accept(counter);

        validator.PrintReport();
        counter.PrintStats();

        Console.WriteLine("Завдання 1: Адаптер");

        // Демонстрація Logger
        var logger = new Logger();
        logger.Log("Console log message");
        logger.Error("Console error message");
        logger.Warn("Console warning message");

        // Демонстрація FileLoggerAdapter
        var fileWriter = new FileWriter();
        ILogger fileLogger = new FileLoggerAdapter(fileWriter);
        fileLogger.Log("File log message");
        fileLogger.Error("File error message");
        fileLogger.Warn("File warning message");

        Console.WriteLine("File logs written to log.txt\n");


        Console.WriteLine("Завдання 2: Декоратор");

        // Воїн з повним спорядженням
        Hero warrior = new Warrior();
        warrior = new Armor(warrior);
        warrior = new Weapon(warrior);
        warrior = new Artifact(warrior);
        Console.WriteLine($"Warrior equipment: {warrior.GetDescription()}");

        // Маг з обмеженим спорядженням
        Hero mage = new Mage();
        mage = new Weapon(mage);
        mage = new Artifact(mage);
        Console.WriteLine($"Mage equipment: {mage.GetDescription()}\n");

        Console.WriteLine("Завдання 3: Міст");

        Shape circle = new Circle(new VectorRenderer());
        circle.Draw();

        Shape square = new Square(new RasterRenderer());
        square.Draw();

        Shape triangle = new Triangle(new RasterRenderer());
        triangle.Draw();

        Console.WriteLine();

               
        Console.WriteLine("Завдання 4: Проксі");

        // Створюємо тестовий файл
        string testFile = "test.txt";
        File.WriteAllLines(testFile, new[] { "Hello, World!", "This is a test file.", "Third line here." });

        // Проксі з логуванням
        ITextReader checker = new SmartTextChecker();
        var data = checker.ReadFile(testFile);

        // Проксі з обмеженням доступу
        ITextReader locker = new SmartTextReaderLocker(@"restricted.*");
        locker.ReadFile("restricted_file.txt");

        Console.WriteLine();

  
        Console.WriteLine("Завдання 5: Компонувальник");

        // Створюємо HTML структуру: <div class="container">Hello, <span class="highlight">World!</span></div>
        var div = new LightElementNode("div", "block");
        div.AddClass("container");
        div.AddChild(new LightTextNode("Hello, "));

        var span = new LightElementNode("span", "inline");
        span.AddClass("highlight");
        span.AddChild(new LightTextNode("World!"));

        div.AddChild(span);

        Console.WriteLine("OuterHTML:");
        Console.WriteLine(div.OuterHTML);
        Console.WriteLine("InnerHTML:");
        Console.WriteLine(div.InnerHTML);
        Console.WriteLine();

       
        
      
        Console.WriteLine(" Завдання 6: Легковаговик ");

        // Текст книги для перетворення
        var bookLines = new[]
        {
            "The Great Book Title",
            "Short line",
            "   This is a quoted line with leading spaces",
            "This is a normal paragraph with more than twenty characters in it.",
            "Another short one",
            "   Another quote",
            "Final paragraph with sufficient length to be considered normal."
        };

        // Створюємо легковаговики для різних типів елементів
        var h1Fly = new LightElementNodeFlyweight("h1");
        var h2Fly = new LightElementNodeFlyweight("h2");
        var blockquoteFly = new LightElementNodeFlyweight("blockquote");
        var pFly = new LightElementNodeFlyweight("p");

        var htmlNodes = new List<LightNode>();

        // Перетворюємо кожен рядок згідно з правилами
        for (int i = 0; i < bookLines.Length; i++)
        {
            string line = bookLines[i];

            if (i == 0) // Перший рядок - h1
            {
                htmlNodes.Add(new LightElementNodeWithFlyweight(h1Fly, line));
            }
            else if (line.Length < 20)
            {
                htmlNodes.Add(new LightElementNodeWithFlyweight(h2Fly, line));
            }
            else if (line.StartsWith(" "))
            {
                htmlNodes.Add(new LightElementNodeWithFlyweight(blockquoteFly, line.Trim()));
            }
            else
            {
                htmlNodes.Add(new LightElementNodeWithFlyweight(pFly, line));
            }
        }

        // Виводимо результат
        Console.WriteLine("HTML representation of book text:");
        foreach (var node in htmlNodes)
        {
            Console.WriteLine(node.OuterHTML);
        }

        Console.WriteLine("\nMemory usage demonstration: All <h2> elements share the same flyweight object.");
        Console.WriteLine($"Number of flyweight objects created: 4 (h1, h2, blockquote, p)");
        Console.WriteLine($"Number of HTML nodes created: {htmlNodes.Count}");
    }
}