using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;


namespace Cobra.Lang {


public interface ICallable {
	// object Call(object[] args);
	object Call(object arg);
}


public class CobraDirectString : Object {

	string _s;

	public CobraDirectString(string s) {
		_s = s;
	}

	public string String {
		get {
			return _s;
		}
	}

}


public class AssertException : Exception {

	protected string   _fileName;
	protected int      _lineNumber;
	protected object[] _expressions;
	protected object   _info;

	public AssertException(string fileName, int lineNumber, object[] expressions, object info)
		: this(fileName, lineNumber, expressions, info, null) {
	}

	public AssertException(string fileName, int lineNumber, object[] expressions, object info, Exception innerExc)
		: base("assert", innerExc) {
		_fileName = fileName;
		_lineNumber = lineNumber;
		_expressions = expressions;
		_info = info;
	}

	public override string Message {
		get {
			StringBuilder sb = new StringBuilder("\n");
			sb.AppendFormat("location = {0}:{1}\n", _fileName, _lineNumber);
			string info = null;
			try {
				info = CobraCore.ToTechString(_info);
			} catch (Exception e) {
				info = "toTechString exception: " + e.Message;
			}
			sb.AppendFormat("info = {0}\n", info);
			int indentLevel = 1;
			int i = 1;
			while (i < _expressions.Length) {
				object item = _expressions[i];
				if (item.Equals(+1)) {
					indentLevel++;
					i++;
				} else if (item.Equals(-1)) {
					indentLevel--;
					i++;
				} else {
					string source = (string)_expressions[i];
					object value = _expressions[i+1];
					CobraDirectString dirStr = value as CobraDirectString;
					string valueString;
					if (dirStr!=null) {
						valueString = dirStr.String;
					} else {
						try {
							valueString = CobraCore.ToTechString(value);
						} catch (Exception e) {
							valueString = "toTechString exception: " + e.Message;
						}
					}
					// for (int x = 0; x < indentLevel*4; x++)
					sb.Append(new String(' ', indentLevel*4));
					sb.AppendFormat("{0} = {1}\n", source, valueString);
					i += 2;
				}
			}
			return sb.ToString();
		}
	}

	public object Info {
		get {
			return _info;
		}
	}

}


public class RequireException : AssertException {

	RequireException _next;

	public RequireException(string fileName, int lineNumber, object[] expressions, object info)
		: this(fileName, lineNumber, expressions, info, null) {
	}

	public RequireException(string fileName, int lineNumber, object[] expressions, object info, Exception innerExc)
		: base(fileName, lineNumber, expressions, info, innerExc) {
	}

	public RequireException Next {
		get {
			return _next;
		}
		set {
			_next = value;
		}
	}

}


public class EnsureException : AssertException {

	public EnsureException(string fileName, int lineNumber, object[] expressions, object info)
		: this(fileName, lineNumber, expressions, info, null) {
	}

	public EnsureException(string fileName, int lineNumber, object[] expressions, object info, Exception innerExc)
		: base(fileName, lineNumber, expressions, info, innerExc) {
	}

}


public class ExpectException : Exception {

	protected Type _expectedExceptionType;
	protected Exception _actualException;

	public ExpectException(Type expectedExceptionType, Exception actualException)
		: base() {
		_expectedExceptionType = expectedExceptionType;
		_actualException = actualException;
	}

	public ExpectException(Type expectedExceptionType, Exception actualException, Exception innerExc)
		: base(null, innerExc) {
		_expectedExceptionType = expectedExceptionType;
		_actualException = actualException;
	}

	public override string Message {
		get {
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("Expecting exception: {0}, but ", _expectedExceptionType.Name);
			if (_actualException==null)
				sb.Append("no exception was thrown.");
			else
				sb.AppendFormat("a different exception was thrown: {0}.", _actualException);
			return sb.ToString();
		}
	}

	public Type ExpectedExceptionType {
		get {
			return _expectedExceptionType;
		}
	}

	public Exception ActualException {
		get {
			return _actualException;
		}
	}

}


public class SliceException : SystemException {

	public SliceException(string msg)
		: base(msg) {
	}

}


static public class CobraImp {

	// Public to Cobra source for the purpose of generated code (not user code).

	// Supports Cobra language features.

	static CobraImp() {
		_printToStack = new Stack<TextWriter>();
		PushPrintTo(Console.Out);
	}

	static public string TypeName(Type t) {
		if (t==null)
			return "nil";
		if (t.IsGenericType) {
			StringBuilder sb = new StringBuilder();
			string[] parts = t.GetGenericTypeDefinition().Name.Split(new char[] {'`'}, 2);
			sb.AppendFormat("{0}<of ", parts[0]);
			string sep = "";
			foreach(Type genArg in t.GetGenericArguments()) {
				sb.AppendFormat("{0}{1}", sep, TypeName(genArg));
				sep = ", ";
			}
			sb.Append(">");
			return sb.ToString();
		}
		if (t==typeof(int))
			return "int";
		if (t==typeof(double))
			return "float";
		if (t==typeof(decimal))
			return "decimal";
		if (t==typeof(bool))
			return "bool";
		if (t==typeof(char))
			return "char";
		return t.Name;
	}

	static public string ToTechString(object x) {
		if (x==null)
			return "nil";
		if (x is bool)
			return (bool)x ? "true" : "false";
		if (x is string) {
			string s = (string)x;
			s = s.Replace("\n", "\\n");
			s = s.Replace("\r", "\\r");
			s = s.Replace("\t", "\\t");
			s = "'" + s + "'";  // TODO: could be more sophisticated with respect to ' and "
			return s;
		}
		if (x is System.Collections.IList) {
			// TODO: should not go into infinite loop for circular references
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("{0}[", TypeName(x.GetType()));
			string sep = "";
			foreach (object item in (System.Collections.IList)x) {
				sb.AppendFormat("{0}{1}", sep, ToTechString(item));
				sep = ", ";
			}
			sb.Append("]");
			return sb.ToString();
		}
		if (x is System.Collections.IDictionary) {
			// TODO: should not go into infinite loop for circular references
			System.Collections.IDictionary idict = (System.Collections.IDictionary)x;
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("{0}{1}", TypeName(x.GetType()), "{");
			string sep = "";
			foreach (object key in idict.Keys) {
				sb.AppendFormat("{0}{1}: {2}", sep, ToTechString(key), ToTechString(idict[key]));
				sep = ", ";
			}
			sb.Append("}");
			return sb.ToString();
		}
		// TODO: For StringBuilder, return StringBuilder'aoeu'
		return x.ToString();
	}

	static public new bool Equals(object a, object b) {
		// Cobra will generate the C# "a==b" when a and b are both primitive types (int, decimal,
		// etc.) But in the event that a and b are statically typed as "Object", that does not
		// mean that equality should stop making sense. Hence, below we cover the cases where
		// a.Equals(b) fails us (there are suprisingly many).

		// decimal is retarded
		if (a is decimal) {
			if (b is decimal)
				return (decimal)a == (decimal)b;
			else if (b is int)
				return (decimal)a == (int)b;
			// TODO: what about other kinds of ints?
		}
		// double is a little retarded too
		if (a is double) {
			if (b is double)
				return (double)a == (double)b;
			else if (b is int)
				return (double)a == (int)b;
		}
		// TODO: probably need to handle all aspects of numeric promotion!
		// Note: IConvertible might be a fast, though imperfect way, of homing in on primitive types (except that string also implements it).
		if (a is char && b is string)
			return Equals((char)a, (string)b);
		else if (a is string && b is char)
			return Equals((char)b, (string)a);
		if (a is IList && b is IList)
			return Equals((IList)a, (IList)b);
		if (a is IDictionary && b is IDictionary)
			return Equals((IDictionary)a, (IDictionary)b);
		// what we really want for objects that can handle it:
		return object.Equals(a, b);
	}

	static public bool Equals(char c, string s) {
		if (s==null)
			return false;
		if (s.Length==1 && c==s[0])
			return true;
		return new string(c, 1) == s;
	}

	static public bool Equals(IList a, IList b) {
		if (a.Count!=b.Count)
			return false;
		int count = a.Count;
		for (int i=0; i<count; i++) {
			if (!CobraImp.Equals(a[i], b[i]))
				return false;
		}
		return true;
	}

	static public bool Equals(IDictionary a, IDictionary b) {
		if (a.Count!=b.Count)
			return false;
		foreach (object key in a.Keys) {
			if (!b.Contains(key))
				return false;
			if (!CobraImp.Equals(a[key], b[key]))
				return false;
		}
		foreach (object key in b.Keys) {
			if (!a.Contains(key))
				return false;
		}
		return true;
	}

	static public bool NotEquals(object a, object b) {
		return !Equals(a, b);
	}

	static public bool In(string a, string b) {
		return b.Contains(a);
	}

	static public bool In(char a, string b) {
		return b.IndexOf(a)!=-1;
	}

	static public bool In<innerType>(innerType a, IList<innerType> b) {
		return b.Contains(a);
	}

	static public bool In<keyType,valueType>(keyType a, IDictionary<keyType,valueType> b) {
		return b.ContainsKey(a);
	}

	static public bool IsTrue(char c) {
		return c!='\0';
	}

	static public bool IsTrue(int i) {
		return i!=0;
	}

	static public bool IsTrue(decimal d) {
		return d!=0;
	}

	static public bool IsTrue(float f) {
		return f!=0;
	}

	static public bool IsTrue(double d) {
		return d!=0;
	}

	static public bool IsTrue(string s) {
		return s!=null;
	}

	static public bool IsTrue(System.Collections.ICollection c) {
		// TODO: does System.Collections.Generics.ICollection inherit the non-generic ICollection?
		// TODO: if a C# file uses both System.Collections and System.Collections.Generics, then what does "ICollection" mean?
		return c!=null;
	}

	static public bool IsTrue(object x) {
		if (x==null)
			return false;
		if (x is bool)
			return (bool)x;
		if (x.Equals(0))
			return false;
		if (x is char)
			return (char)x!='\0';
		if (x is decimal)
			return (decimal)x!=0;  // can't believe x.Equals(0) above doesn't work for decimal. *sigh*
		return true;
	}

	static public bool Is(object a, object b) {
		return object.ReferenceEquals(a, b);
	}

	static public bool IsNot(object a, object b) {
		return !object.ReferenceEquals(a, b);
	}

	static public bool Is(Enum a, Enum b) {
		return a.Equals(b);
		//return a==b;  this returns false when you would expect true!
	}

	static public object ToOrNil<T>(object x)
		where T : struct {
		// using this method ensures that x is only evaluated once in the generated C# code for
		// x to? type
		if (x is T || x is T?)
			return x;
		else
			return null;
	}

	public delegate TOut ForGet<TIn, TOut>(TIn value);

	public delegate bool ForWhereGet<TIn, TOut>(TIn inValue, out TOut outValue);

	static public IList<TOut> For<TIn, TOut>(IList<TIn> list, ForGet<TIn, TOut> forGet) {
		// TODO: if possible, it might be nice to get the generic type of the list coming in and then make a constructed type from it with TOut.
		List<TOut> results = new List<TOut>(list.Count);
		foreach (TIn item in list)
			results.Add(forGet(item));
		return results;
	}

	static public IList<TOut> For<TIn, TOut>(IList<TIn> list, ForWhereGet<TIn, TOut> forWhereGet) {
		IList<TOut> results = new List<TOut>();
		foreach (TIn item in list) {
			TOut value;
			if (forWhereGet(item, out value))
				results.Add(value);
		}
		return results;
	}

	static private void ProcessGetSliceArgs(int count, ref int? start, ref int? stop, ref int? step) {
		if (start==null)
			start = 0;
		if (start<0) {
			start += count;
			if (start<0)
				throw new IndexOutOfRangeException(string.Format("Start is {0} for string of length {1}.", start-count, count));
		} else if (start>count) {
			throw new IndexOutOfRangeException(string.Format("Start is {0} for string of length {1}.", start, count));
		}
		if (stop==null)
			stop = count;
		if (stop<0) {
			stop += count;
			if (stop<0)
				throw new IndexOutOfRangeException(string.Format("Stop is {0} for string of length {1}.", stop-count, count));
		} else if (stop>count) {
			throw new IndexOutOfRangeException(string.Format("Stop is {0} for string of length {1}.", stop, count));
		}
		if (step==null)
			step = 1;
		if (step==0)
			throw new SliceException(string.Format("Cannot use a step of zero for slices."));
		// step is negative
		if (start>stop)
			throw new SliceException(string.Format("start={0} is less than start={1} for a negative step.", start, stop));
		if (step>0) {
			if (step!=1)
				throw new SliceException(string.Format("step={0}, but only a step of 1 is currently supported", step));
			if (stop<start)
				throw new SliceException(string.Format("stop={0} is less than start={1} for a positive step.", stop, start));
		} else {
			if (step<0)
				throw new SliceException(string.Format("step={0}, but only a step of 1 is currently supported.", step));
		}
		// at this point start, stop and step or non-null and
		// stop>=start and step==1
	}

	static public string GetSlice(string s, int? start, int? stop, int? step) {
		if (s==null)
			throw new NullReferenceException("Cannot slice null.");
		ProcessGetSliceArgs(s.Length, ref start, ref stop, ref step);
		return s.Substring(start.Value, stop.Value-start.Value);
	}

	static public System.Collections.IList GetSlice(System.Collections.IList list, int? start, int? stop, int? step) {
		if (list==null)
			throw new NullReferenceException("Cannot slice null.");
		ProcessGetSliceArgs(list.Count, ref start, ref stop, ref step);
		IList slice = (IList)Activator.CreateInstance(list.GetType());
		for (int i=start.Value; i<stop.Value; i+=step.Value)
			slice.Add(list[i]);
		return slice;
	}

	static public System.Collections.ArrayList GetSlice(System.Collections.ArrayList list, int? start, int? stop, int? step) {
		if (list==null)
			throw new NullReferenceException("Cannot slice null.");
		ProcessGetSliceArgs(list.Count, ref start, ref stop, ref step);
		return list.GetRange(start.Value, stop.Value-start.Value);
	}

	static public IList<T> GetSlice<T>(IList<T> list, int? start, int? stop, int? step) {
		if (list==null)
			throw new NullReferenceException("Cannot slice null.");
		ProcessGetSliceArgs(list.Count, ref start, ref stop, ref step);
		IList<T> slice = (IList<T>)Activator.CreateInstance(list.GetType());
		for (int i=start.Value; i<stop.Value; i+=step.Value)
			slice.Add(list[i]);
		return slice;
	}

	static public List<T> GetSlice<T>(List<T> list, int? start, int? stop, int? step) {
		if (list==null)
			throw new NullReferenceException("Cannot slice null.");
		ProcessGetSliceArgs(list.Count, ref start, ref stop, ref step);
		return list.GetRange(start.Value, stop.Value-start.Value);
	}

	static private Stack<TextWriter> _printToStack;

	static public void PushPrintTo(TextWriter tw) {
		_printToStack.Push(tw);
	}

	static public void PopPrintTo() {
		_printToStack.Pop();
	}

	static public void PrintLine() {
		_printToStack.Peek().WriteLine();
	}

	static public void PrintLine(string s) {
		_printToStack.Peek().WriteLine(s);
	}

	static public void PrintStop() {
	}

	static public void PrintStop(string s) {
		_printToStack.Peek().Write(s);
	}

	static public string MakeString(params string[] args) {
		StringBuilder sb = new StringBuilder();
		foreach (object arg in args)
			sb.Append(arg);
		return sb.ToString();
	}

	static public string ToString(object x) {
		if (x==null)
			return "nil";
		if (x is bool)
			return (bool)x ? "true" : "false";
		return x.ToString();
	}

	static public string ToString(object x, string format) {
		if (x==null)
			return "nil";
		if (x is bool)
			return (bool)x ? "true" : "false";
		// there's probably a better way to do this:
		format = "{0:" + format + "}";
		return string.Format(format, x);
	}

	static public List<innerType> MakeList<innerType>(Type listType, params innerType[] args) {
		return new List<innerType>(args);
	}

	static public Dictionary<keyType,valueType> MakeDict<keyType,valueType>(Type dictType, params object[] args) {
		Dictionary<keyType,valueType> d = new Dictionary<keyType,valueType>();
		for (int i=0; i<args.Length; i+=2)
			d.Add((keyType)args[i], (valueType)args[i+1]);
		return d;
	}


	/// Show test progress

	static private bool _showTestProgress = false;

	static public bool ShowTestProgress {
		get {
			return _showTestProgress;
		}
		set {
			_showTestProgress = value;
		}
	}

	static private TextWriter _testProgressWriter = null;

	static public TextWriter TestProgressWriter {
		get {
			return _testProgressWriter==null ? Console.Out : _testProgressWriter;
		}
		set {
			_testProgressWriter = value;
		}
	}

	static public void TestBegan(string className) {
		if (ShowTestProgress) {
			TestProgressWriter.WriteLine("Testing {0}...", className);
			TestProgressWriter.Flush();
		}
	}

	static public void TestEnded(string className) {
		if (ShowTestProgress) {
			TestProgressWriter.WriteLine("Completed testing of {0}.\n", className);
			TestProgressWriter.Flush();
		}
	}


	/// Super Stack Trace!

	static public bool HasSuperStackTrace {
		get {
			return _badStackCopy!=null;
		}
	}

	static private Stack<CobraFrame> _superStack = new Stack<CobraFrame>();
	static private Stack<CobraFrame> _badStackCopy = null;

	static public void PushFrame(string declClassName, string methodName, params object[] args) {
		_superStack.Push(new CobraFrame(declClassName, methodName, args));
	}

	static public void SetLine(int lineNum) {
		_superStack.Peek().SetLine(lineNum);
	}

	static public T SetLocal<T>(string name, T value) {
		_superStack.Peek().SetLocal(name, value);
		return value;
	}

	static public void CaughtUncaughtException() {
		if (_badStackCopy==null) {
			_badStackCopy = new Stack<CobraFrame>(_superStack.Count);
			foreach (CobraFrame frame in _superStack)
				_badStackCopy.Push(frame.Copy());
		}
	}

	static public void PopFrame() {
		_superStack.Pop();
	}

	static public void DumpStack() {
		DumpStack(Console.Out);
	}

	static public void DumpStack(TextWriter tw) {
		tw.WriteLine("Stack trace:");
		if (_badStackCopy==null)
			tw.WriteLine("No bad stack.");
		int i = 0;
		foreach (CobraFrame frame in _badStackCopy) {
			frame.Dump(tw, i);
			i++;
		}
	}

	// Dynamic Binding

	static private readonly BindingFlags PropertyFlags = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.GetProperty;
	static private readonly BindingFlags FieldFlags = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.GetField;

	static public object GetPropertyValue(Object obj, string propertyName) {
		if (obj==null)
			throw new ArgumentNullException("obj");
		if (propertyName==null)
			throw new ArgumentNullException("propertyName");
		Type type = obj.GetType();
		PropertyInfo pi = type.GetProperty(propertyName, PropertyFlags);
		if (pi!=null) {
			if (pi.CanRead) {
				return pi.GetValue(obj, null);
			} else {
				throw new CannotReadPropertyException(obj, propertyName, type);
			}
		} else {
			FieldInfo fi = type.GetField(propertyName, FieldFlags);
			if (fi!=null)
				return fi.GetValue(obj);
			throw new UnknownMemberException(obj, propertyName, type);
		}

	}

	static public object SetPropertyValue(Object obj, string propertyName, Object value) {
		if (obj==null)
			throw new ArgumentNullException("obj");
		if (propertyName==null)
			throw new ArgumentNullException("propertyName");
		Type type = obj.GetType();
		PropertyInfo pi = type.GetProperty(propertyName, PropertyFlags);
		if (pi!=null) {
			if (pi.CanWrite) {
				pi.SetValue(obj, value, null);
				return value;
			} else {
				throw new CannotWritePropertyException(obj, propertyName, type);
			}
		} else {
			FieldInfo fi = type.GetField(propertyName, FieldFlags);
			if (fi!=null) {
				fi.SetValue(obj, value);
				return value;
			}
			throw new UnknownMemberException(obj, propertyName, type);
		}
	}

}


internal class CobraFrame {

	protected string _declClassName;
	protected string _methodName;
	protected int _lineNum;
	protected object[] _args;
	protected Dictionary<string, object> _locals;
	protected List<string> _localNamesInOrder;

	// args should have the arg names embedded: "x", x, "y", y
	public CobraFrame(string declClassName, string methodName, params object[] args) {
		_declClassName = declClassName;
		_methodName = methodName;
		_args = (object[])args.Clone();
		_locals = new Dictionary<string, object>();
		_localNamesInOrder = new List<string>();
		for (int j=0; j<_args.Length; j+=2)
			SetLocal((string)_args[j], _args[j+1]);
	}

	public void SetLine(int lineNum) {
		_lineNum = lineNum;
	}

	public void SetLocal(string name, object value) {
		if (!_locals.ContainsKey(name))
			_localNamesInOrder.Add(name);
		_locals[name] = value;
	}

	public void Dump(TextWriter tw, int i) {
		int nameWidth = 8;
		tw.WriteLine("\n    {0}. {1}", i, this);
		tw.WriteLine("        args");
		for (int j=0; j<_args.Length; j+=2) {
			tw.Write("               {0} = ", ((string)_args[j]).PadRight(nameWidth));
			string s;
			try {
				s = CobraCore.ToTechString(_args[j+1]);
			} catch (Exception e) {
				s = "ToString() Exception: " + e.Message;
			}
			tw.WriteLine(s);
		}
		tw.WriteLine("        locals");
		foreach (string name in _localNamesInOrder) {
			if (name=="this")
				continue;
			tw.Write("               {0} = ", name.PadRight(nameWidth));
			string s;
			try {
				s = CobraCore.ToTechString(_locals[name]);
			} catch (Exception e) {
				s = "ToString() Exception: " + e.Message;
			}
			tw.WriteLine(s);
		}
	}

	public override string ToString() {
		return string.Format("def {0}.{1} at line {2}", _declClassName, _methodName, _lineNum);
	}

	public CobraFrame Copy() {
		return (CobraFrame)MemberwiseClone();
	}

}

}
