using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Unigram.Core.Media
{
    class JSExtractor
    {
        private static readonly Regex exprParensPattern = new Regex("[()]", RegexOptions.Compiled);
        private static readonly Regex stmtVarPattern = new Regex("var\\s", RegexOptions.Compiled);
        private static readonly Regex stmtReturnPattern = new Regex("return(?:\\s+|$)", RegexOptions.Compiled);

        private static readonly String exprName = "[a-zA-Z_$][a-zA-Z_$0-9]*";

        private readonly List<string> _codeLines = new List<string>();

        private readonly string _jsCode;
        private readonly string[] _operators = { "|", "^", "&", ">>", "<<", "-", "+", "%", "/", "*" };
        private readonly string[] _assign_operators = { "|=", "^=", "&=", ">>=", "<<=", "-=", "+=", "%=", "/=", "*=", "=" };

        public JSExtractor(string js)
        {
            _jsCode = js;
        }

        private void InterpretExpression(string expr, Dictionary<string, string> localVars, int allowRecursion)
        {
            expr = expr.Trim();
            Match matcher;
            if (string.IsNullOrEmpty(expr))
            {
                return;
            }
            if (expr[0] == '(')
            {
                int parens_count = 0;
                matcher = exprParensPattern.Match(expr);
                while (matcher.Success)
                {
                    String group = matcher.Groups[0].Value;
                    if (group.IndexOf('0') == '(')
                    {
                        parens_count++;
                    }
                    else
                    {
                        parens_count--;
                        if (parens_count == 0)
                        {
                            String sub_expr = expr.Substring(1, matcher.Index - 1);
                            InterpretExpression(sub_expr, localVars, allowRecursion);
                            String remaining_expr = expr.Substring(matcher.Index + matcher.Length).Trim();
                            if (string.IsNullOrEmpty(remaining_expr))
                            {
                                return;
                            }
                            else
                            {
                                expr = remaining_expr;
                            }
                            break;
                        }
                    }

                    matcher = matcher.NextMatch();
                }
                if (parens_count != 0)
                {
                    throw new Exception(String.Format("Premature end of parens in {0}", expr));
                }
            }

            for (int a = 0; a < _assign_operators.Length; a++)
            {
                String func = _assign_operators[a];
                matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "(?x)({0})(?:\\[([^\\]]+?)\\])?\\s*{1}(.*)$", exprName, Regex.Escape(func))).Match(expr);
                if (!matcher.Success)
                {
                    continue;
                }
                InterpretExpression(matcher.Groups[3].Value, localVars, allowRecursion - 1);
                String index = matcher.Groups[2].Value;
                if (!string.IsNullOrEmpty(index))
                {
                    InterpretExpression(index, localVars, allowRecursion);
                }
                else
                {
                    localVars[matcher.Groups[1].Value] = "";
                }
                return;
            }

            if (int.TryParse(expr, out int ignore))
            {
                return;
            }

            matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "(?!if|return|true|false)({0})$", exprName)).Match(expr);
            if (matcher.Success)
            {
                return;
            }

            if (expr[0] == '"' && expr[expr.Length - 1] == '"')
            {
                return;
            }
            //try
            //{
            //    JsonObject.Parse(expr).Stringify();
            //    return;
            //}
            //catch (Exception e)
            //{
            //    //ignore
            //}

            if (JsonObject.TryParse(expr, out JsonObject ignore2))
            {
                return;
            }

            matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "({0})\\.([^(]+)(?:\\(+([^()]*)\\))?$", exprName)).Match(expr);
            if (matcher.Success)
            {
                String variable = matcher.Groups[1].Value;
                String member = matcher.Groups[2].Value;
                String arg_str = matcher.Groups[3].Value;
                //if (localVars.get(variable) == null)
                //{
                //    extractObject(variable);
                //}
                if (!localVars.ContainsKey(variable) || localVars[variable] == null)
                {
                    ExtractObject(variable);
                }
                if (arg_str == null)
                {
                    return;
                }
                if (expr[expr.Length - 1] != ')')
                {
                    throw new Exception("last char not ')'");
                }
                String[] argvals;
                if (arg_str.Length != 0)
                {
                    String[] args = arg_str.Split(',');
                    for (int a = 0; a < args.Length; a++)
                    {
                        InterpretExpression(args[a], localVars, allowRecursion);
                    }
                }
                return;
            }

            matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "({0})\\[(.+)\\]$", exprName)).Match(expr);
            if (matcher.Success)
            {
                Object val = localVars[matcher.Groups[1].Value];
                InterpretExpression(matcher.Groups[2].Value, localVars, allowRecursion - 1);
                return;
            }

            for (int a = 0; a < _operators.Length; a++)
            {
                String func = _operators[a];
                matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "(.+?){0}(.+)", Regex.Escape(func))).Match(expr);
                if (!matcher.Success)
                {
                    continue;
                }
                bool[] abort = new bool[1];
                InterpretStatement(matcher.Groups[1].Value, localVars, abort, allowRecursion - 1);
                if (abort[0])
                {
                    throw new Exception(String.Format("Premature left-side return of {0} in {1}", func, expr));
                }
                InterpretStatement(matcher.Groups[2].Value, localVars, abort, allowRecursion - 1);
                if (abort[0])
                {
                    throw new Exception(String.Format("Premature right-side return of {0} in {1}", func, expr));
                }
            }

            matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "^({0})\\(([a-zA-Z0-9_$,]*)\\)$", exprName)).Match(expr);
            if (matcher.Success)
            {
                String fname = matcher.Groups[1].Value;
                ExtractFunction(fname);
            }
            throw new Exception(String.Format("Unsupported JS expression {0}", expr));
        }

        private void InterpretStatement(string stmt, Dictionary<string, string> localVars, bool[] abort, int allowRecursion)
        {
            if (allowRecursion < 0)
            {
                throw new Exception("recursion limit reached");
            }
            abort[0] = false;
            stmt = stmt.Trim();
            var matcher = stmtVarPattern.Match(stmt);
            String expr;
            if (matcher.Success)
            {
                expr = stmt.Substring(matcher.Groups[0].Value.Length);
            }
            else
            {
                matcher = stmtReturnPattern.Match(stmt);
                if (matcher.Success)
                {
                    expr = stmt.Substring(matcher.Groups[0].Value.Length);
                    abort[0] = true;
                }
                else
                {
                    expr = stmt;
                }
            }
            InterpretExpression(expr, localVars, allowRecursion);
        }

        private Dictionary<string, object> ExtractObject(string objname)
        {
            Dictionary<String, Object> obj = new Dictionary<String, Object>();
            //                                                                                         ?P<fields>
            var matcher = new Regex(String.Format(CultureInfo.InvariantCulture, "(?:var\\s+)?{0}\\s*=\\s*\\{{\\s*(([a-zA-Z$0-9]+\\s*:\\s*function\\(.*?\\)\\s*\\{{.*?\\}}(?:,\\s*)?)*)\\}}\\s*;", Regex.Escape(objname))).Match(_jsCode);
            String fields = null;
            while (matcher.Success)
            {
                String code = matcher.Value;
                fields = matcher.Groups[2].Value;
                if (string.IsNullOrEmpty(fields))
                {
                    matcher.NextMatch();
                    continue;
                }
                if (!_codeLines.Contains(code))
                {
                    _codeLines.Add(matcher.Value);
                }
                break;
            }
            //                          ?P<key>                            ?P<args>     ?P<code>
            matcher = new Regex("([a-zA-Z$0-9]+)\\s*:\\s*function\\(([a-z,]+)\\)\\{([^}]+)\\}").Match(fields);
            while (matcher.Success)
            {
                String[] argnames = matcher.Groups[2].Value.Split(',');
                BuildFunction(argnames, matcher.Groups[3].Value);
                matcher = matcher.NextMatch();
            }
            return obj;
        }

        private void BuildFunction(string[] argNames, string funcCode)
        {
            Dictionary<String, String> localVars = new Dictionary<String, String>();
            for (int a = 0; a < argNames.Length; a++)
            {
                localVars[argNames[a]] = "";
            }
            String[] stmts = funcCode.Split(';');
            bool[] abort = new bool[1];
            for (int a = 0; a < stmts.Length; a++)
            {
                InterpretStatement(stmts[a], localVars, abort, 100);
                if (abort[0])
                {
                    return;
                }
            }
        }

        public string ExtractFunction(string funcName)
        {
            try
            {
                var quote = Regex.Escape(funcName);
                var funcPattern = new Regex(String.Format(CultureInfo.InvariantCulture, "(?x)(?:function\\s+{0}|[{{;,]\\s*{0}\\s*=\\s*function|var\\s+{0}\\s*=\\s*function)\\s*\\(([^)]*)\\)\\s*\\{{([^}}]+)\\}}", quote, quote, quote));
                var matcher = funcPattern.Match(_jsCode);
                if (matcher.Success)
                {
                    var group = matcher.Value;
                    if (!_codeLines.Contains(group))
                    {
                        _codeLines.Add(group + ";");
                    }

                    BuildFunction(matcher.Groups[1].Value.Split(','), matcher.Groups[2].Value);
                }
            }
            catch (Exception e)
            {
                _codeLines.Clear();
            }

            return string.Join(string.Empty, _codeLines);
        }
    }
}
