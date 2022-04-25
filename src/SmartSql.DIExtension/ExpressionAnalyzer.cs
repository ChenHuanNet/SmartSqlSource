using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ServiceStack;

namespace SmartSql.DIExtension
{
    public class ExpressionAnalyzer : ExpressionVisitor
    {
        /// <summary>
        /// 表达式所有参数集合
        /// </summary>
        private Dictionary<string, object> _params;

        /// <summary>
        /// 解析结果
        /// </summary>
        public AnalysisData ResultData { get; set; }

        /// <summary>
        /// 命名参数别名
        /// </summary>
        private string _argName = "TAB";

        public string TableAlias => _argName;

        /// <summary>
        /// 系统默认基本类型名称
        /// </summary>
        private const string _defaultBasicTypeName =
            @"String|Boolean|Double|Int32|Int64|Int16|Single|DateTime|Decimal|Char|Object|Guid";

        /// <summary>
        /// 构造   LastUpdateDate：2022-01-06 15:46:05.495  Author：Lingbug
        /// </summary>
        public ExpressionAnalyzer()
        {
            //初始化
            _argName = "TAB";
            _params = new Dictionary<string, object>();
            ResultData = new AnalysisData()
            {
                TableList = new Dictionary<string, AnalysisTable>(),
                StackList = new List<string>(),
                ParamList = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 构造   LastUpdateDate：2022-01-06 15:46:55.254  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        public ExpressionAnalyzer(LambdaExpression exp)
        {
            //校验
            if (exp == null) return;
            //读取参数
            AppendParams(GetChildValue(exp.Body), _params);
            foreach (var item in exp.Parameters)
            {
                //解析表
                AnalysisTables(item);
            }

            //解析表达式
            AnalysisExpression(exp.Body);
        }

        public void Analyze(LambdaExpression exp)
        {
            //校验
            if (exp == null) return;
            //读取参数
            // AppendParams(GetChildValue(exp.Body), _params);
            // foreach (var item in exp.Parameters)
            // {
            //     //解析表
            //     AnalysisTables(item);
            // }

            //解析表达式
            AnalysisExpression(exp.Body);
            //记录日志
            Log(this.ToJson());
            //记录日志
            // Log(ResultData.StackList.JoinLingbug(" "));
        }

        /// <summary>
        /// 解析表达式   LastUpdateDate：2022-01-06 16:21:26.770  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="isLeftChild"></param>
        private void AnalysisExpression(Expression exp, bool isLeftChild = true)
        {
            //校验
            if (exp == null) return;
            switch (exp.NodeType)
            {
                case ExpressionType.AndAlso:
                    //开头
                    ResultData.StackList.Add("(");
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //结尾
                    ResultData.StackList.Add(")");
                    //拼接
                    ResultData.StackList.Add("AND");
                    //开头
                    ResultData.StackList.Add("(");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //结尾
                    ResultData.StackList.Add(")");
                    //中止
                    break;
                case ExpressionType.OrElse:
                    //开头
                    ResultData.StackList.Add("(");
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //结尾
                    ResultData.StackList.Add(")");
                    //拼接
                    ResultData.StackList.Add("OR");
                    //开头
                    ResultData.StackList.Add("(");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //结尾
                    ResultData.StackList.Add(")");
                    //中止
                    break;
                case ExpressionType.Equal:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add("=");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.NotEqual:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add("!=");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add(">=");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.GreaterThan:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add(">");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.LessThan:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add("<");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.LessThanOrEqual:
                    //递归
                    AnalysisExpression(GetChildExpression(exp));
                    //拼接
                    ResultData.StackList.Add("<=");
                    //递归
                    AnalysisExpression(GetChildExpression(exp, false), false);
                    //中止
                    break;
                case ExpressionType.Call:
                    //类型转换
                    var imExp = exp as MethodCallExpression;
                    if (imExp.Object == null)
                    {
                        //方法
                        var method = imExp.Method;
                        if (IsNullOrEmptyMethod(method))
                        {
                            if (imExp.Arguments.Count > 0)
                            {
                                string paramName = imExp.Arguments[0].ToString();
                                string rr = $" {paramName} is not null and {paramName} <>'' ";
                                //存储
                                ResultData.StackList.Add(rr);

                                // //递归
                                // AnalysisExpression(imExp.Arguments[0], false);
                            }
                        }
                        else if (IsListContain(method))
                        {
                            if (imExp.Arguments.Count > 0)
                            {
                                var paramName = imExp.Arguments[1].ToString();
                                string rr = $" {paramName} in ( ";
                                ResultData.StackList.Add(rr);

                                object[] data = Expression.Lambda(imExp.Arguments[0]).Compile().DynamicInvoke()
                                    .ConvertTo<object[]>();

                                object item = data[0];


                                var vType = item.GetType();

                                string val = string.Join(",", data);
                                if (vType == typeof(DateTime))
                                {
                                    val = string.Join("','", data);
                                }
                                else if (vType.IsValueType)
                                {
                                    val = string.Join(",", data);
                                }
                                else
                                {
                                    List<string> dList = new List<string>();
                                    foreach (var d in data)
                                    {
                                        string dItem = d.ToString();
                                        if (d.ToString().Contains("'"))
                                        {
                                            dItem = d.ToString().Replace("'", "''");
                                        }

                                        dList.Add(dItem);
                                    }

                                    val = string.Join(",", dList);
                                }


                                ResultData.StackList.Add(val);

                                ResultData.StackList.Add(" ) ");
                            }
                        }
                    }
                    else
                    {
                        //方法
                        var method = imExp.Method;
                        //递归
                        AnalysisExpression(imExp.Object);
                        if (IsStartsWithMethod(method))
                        {
                            //拼接
                            ResultData.StackList.Add("LIKE '");
                            if (imExp.Arguments.Count > 0)
                            {
                                //递归
                                AnalysisExpression(imExp.Arguments[0], false);

                                string val = ResultData.StackList[ResultData.StackList.Count - 1];
                                Regex r = new Regex(@"\'(.+)\'");
                                string s1 = r.Match(val).Groups[1].Value;
                                ResultData.StackList[ResultData.StackList.Count - 1] = $"'{s1}%'";
                                //结尾
                            }
                        }

                        if (IsEndsWithMethod(method))
                        {
                            //拼接
                            ResultData.StackList.Add("LIKE ");
                            if (imExp.Arguments.Count > 0)
                            {
                                //开头
                                //递归
                                AnalysisExpression(imExp.Arguments[0], false);
                                string val = ResultData.StackList[ResultData.StackList.Count - 1];
                                Regex r = new Regex(@"\'(.+)\'");
                                string s1 = r.Match(val).Groups[1].Value;
                                ResultData.StackList[ResultData.StackList.Count - 1] = $"'%{s1}'";
                            }
                        }

                        if (IsContainsMethod(method))
                        {
                            //拼接
                            ResultData.StackList.Add("LIKE ");
                            if (imExp.Arguments.Count > 0)
                            {
                                //开头
                                //递归
                                AnalysisExpression(imExp.Arguments[0], false);
                                string val = ResultData.StackList[ResultData.StackList.Count - 1];

                                Regex r = new Regex(@"\'(.+)\'");
                                string s1 = r.Match(val).Groups[1].Value;
                                ResultData.StackList[ResultData.StackList.Count - 1] = $"'%{s1}%'";
                                //结尾
                            }
                        }


                        if (IsListContain(method))
                        {
                            if (imExp.Arguments.Count > 0)
                            {
                                ResultData.StackList.RemoveAt(ResultData.StackList.Count - 1);
                                var paramName = imExp.Arguments[0].ToString();
                                string rr = $" {paramName} in ( ";
                                ResultData.StackList.Add(rr);

                                object[] data = Expression.Lambda(imExp.Object).Compile().DynamicInvoke()
                                    .ConvertTo<object[]>();

                                object item = data[0];


                                var vType = item.GetType();

                                string val = string.Join(",", data);
                                if (vType == typeof(DateTime))
                                {
                                    val = string.Join("','", data);
                                }
                                else if (vType.IsValueType)
                                {
                                    val = string.Join(",", data);
                                }
                                else
                                {
                                    List<string> dList = new List<string>();
                                    foreach (var d in data)
                                    {
                                        string dItem = d.ToString();
                                        if (d.ToString().Contains("'"))
                                        {
                                            dItem = d.ToString().Replace("'", "''");
                                        }

                                        dList.Add(dItem);
                                    }

                                    val = string.Join(",", dList);
                                }


                                ResultData.StackList.Add(val);

                                ResultData.StackList.Add(" ) ");
                            }
                        }
                    }

                    //中止
                    break;
                case ExpressionType.MemberAccess:
                    if (isLeftChild)
                    {
                        //解析表
                        AnalysisTables(exp);
                        //类型转换
                        var mberExp = exp as MemberExpression;
                        //获取变量名
                        var parentName = GetExpressionName(mberExp.Expression);
                        if (!string.IsNullOrWhiteSpace(parentName))
                        {
                            //存储
                            ResultData.StackList.Add(string.Format("[{0}].{1}", parentName, GetExpressionName(exp)));
                            //中止
                            break;
                        }

                        //存储
                        ResultData.StackList.Add(GetExpressionName(exp));
                    }
                    else
                    {
                        //获取参数名
                        var paramName = GetParamName(exp);
                        //存储
                        ResultData.ParamList.Add(paramName, _params.ContainsKey(paramName) ? _params[paramName] : null);
                        //存储
                        object data = Expression.Lambda(exp).Compile().DynamicInvoke();
                        string val = data.ToString();
                        if (data.GetType() == typeof(DateTime))
                        {
                            val = $"'{val}'";
                        }
                        else if (data.GetType().IsValueType)
                        {
                        }
                        else
                        {
                            val = $"'{val}'";
                        }

                        ResultData.StackList.Add(val);
                    }

                    //中止
                    break;
                case ExpressionType.Constant:
                    //类型转换
                    var constent = exp as ConstantExpression;
                    if (constent.Value == null)
                    {
                        //拿到最后一个
                        var op = ResultData.StackList.ElementAt(ResultData.StackList.Count - 1);
                        //移除
                        ResultData.StackList.RemoveAt(ResultData.StackList.Count - 1);
                        //存储
                        ResultData.StackList.Add(op == "=" ? "IS NULL" : "IS NOT NULL");
                        //中止
                        break;
                    }

                    //读取类型
                    var tValue = constent.Value.GetType();
                    if (tValue == typeof(string))
                    {
                        //存储
                        ResultData.StackList.Add(string.Format("'{0}'", constent.Value));
                        //中止
                        break;
                    }

                    if (tValue == typeof(bool))
                    {
                        if (ResultData.StackList.Count > 0)
                        {
                            //类型转换
                            var value = Convert.ToBoolean(constent.Value);
                            //存储
                            ResultData.StackList.Add(string.Format("{0}", value ? "1" : "0"));
                        }

                        //中止
                        break;
                    }

                    //存储
                    ResultData.StackList.Add(string.Format("{0}", constent.Value));
                    //中止
                    break;
                case ExpressionType.Convert:
                    //类型转换
                    var uExp = exp as UnaryExpression;
                    //递归
                    AnalysisExpression(uExp.Operand, isLeftChild);
                    //中止
                    break;
                case ExpressionType.New:
                    //类型转换
                    var newExp = exp as NewExpression;
                    for (int i = 0; i < newExp.Arguments.Count; i++)
                    {
                        //递归
                        AnalysisExpression(newExp.Arguments[i]);
                        //存储
                        ResultData.StackList.Add("AS");
                        //存储
                        ResultData.StackList.Add(string.Format("'{0}'", newExp.Members[i].Name));
                    }

                    //中止
                    break;
                case ExpressionType.Not:
                    //类型转换
                    var notExp = exp as UnaryExpression;
                    //递归
                    AnalysisExpression(notExp.Operand);
                    //中止
                    break;
                case ExpressionType.Parameter:
                    //提示
                    throw new Exception("ExpressionType.Parameter:");
                default:
                    //打印
                    Log($"AnalysisExpression - 未对该节点类型做任何处理NodeType = {exp.NodeType}");
                    //中止
                    break;
            }
        }

        /// <summary>
        /// 获取子节点   LastUpdateDate：2022-01-06 16:26:42.795  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="getLeft"></param>
        /// <returns></returns>
        private Expression GetChildExpression(Expression exp, bool getLeft = true)
        {
            //校验
            if (exp == null) return null;
            //类型名称
            var typeName = exp.GetType().Name;
            switch (typeName)
            {
                case "BinaryExpression":
                case "LogicalBinaryExpression":
                case "MethodBinaryExpression":
                    //类型转换
                    var bExp = exp as BinaryExpression;
                    //返回
                    return getLeft ? bExp.Left : bExp.Right;
                case "PropertyExpression":
                case "FieldExpression":
                    //返回
                    return exp as MemberExpression;
                case "UnaryExpression":
                    //返回
                    return exp as UnaryExpression;
                case "ConstantExpression":
                    //返回
                    return exp as ConstantExpression;
                case "InstanceMethodCallExpressionN":
                    //返回
                    return exp as MethodCallExpression;
                default:
                    //打印
                    Log($"GetChildExpression - 未对该类型做任何处理typeName = {typeName}");
                    //返回
                    return null;
            }
        }

        /// <summary>
        /// 获取变量名   LastUpdateDate：2022-01-06 16:36:48.112  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        private string GetExpressionName(Expression exp)
        {
            //校验
            if (exp == null) return string.Empty;
            //类型名称
            var typeName = exp.GetType().Name;
            switch (typeName)
            {
                case "PropertyExpression":
                case "FieldExpression":
                    //类型转换
                    var mberExp = exp as MemberExpression;
                    //返回
                    return string.Format("{0}", mberExp.Member.Name);
                case "TypedParameterExpression":
                    //赋值
                    _argName = JObject.Parse(exp.ToJson())["Name"]?.ToString();
                    //返回
                    return _argName;
                default:
                    //打印
                    Log($"GetExpressionName - 未对该类型做任何处理typeName = {typeName}");
                    //返回
                    return string.Empty;
            }
        }

        /// <summary>
        /// 获取参数名   LastUpdateDate：2022-01-06 16:37:50.889  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        private string GetParamName(Expression exp)
        {
            //校验
            if (exp == null) return string.Empty;
            //类型名称
            var typeName = exp.GetType().Name;
            switch (typeName)
            {
                case "PropertyExpression":
                case "FieldExpression":
                    //类型转换
                    var mberExp = exp as MemberExpression;
                    //返回
                    return string.Format("@{0}", mberExp.Member.Name);
                case "TypedParameterExpression":
                    //类型转换
                    var texp = exp as ParameterExpression;
                    //返回
                    return string.Format("@{0}", texp.Name);
                default:
                    //打印
                    Log($"GetParamName - 未对该类型做任何处理typeName = {typeName}");
                    //返回
                    return string.Empty;
            }
        }

        /// <summary>
        /// 解析并存储表信息   LastUpdateDate：2022-01-06 16:20:31.442  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        private void AnalysisTables(Expression exp)
        {
            //校验
            if (exp == null) return;
            //类型名称
            var typeName = exp.GetType().Name;
            switch (typeName)
            {
                case "PropertyExpression":
                case "FieldExpression":
                    //类型转换
                    var mberExp = exp as MemberExpression;
                    if (!IsDefaultType(mberExp.Type) && !ResultData.TableList.ContainsKey(mberExp.Member.Name))
                    {
                        //存储
                        ResultData.TableList.Add(mberExp.Member.Name, new AnalysisTable()
                        {
                            Name = mberExp.Type.Name,
                            TableType = mberExp.Type,
                            IsMainTable = false
                        });
                    }

                    //递归
                    AnalysisTables(mberExp.Expression);
                    //中止
                    break;
                case "TypedParameterExpression":
                    //类型转换
                    var texp = exp as ParameterExpression;
                    if (!IsDefaultType(texp.Type) && !ResultData.TableList.ContainsKey(_argName))
                    {
                        //存储
                        ResultData.TableList.Add(_argName, new AnalysisTable()
                        {
                            Name = texp.Type.Name,
                            TableType = texp.Type,
                            IsMainTable = true
                        });
                    }

                    //中止
                    break;
                default:
                    //打印
                    Log($"AnalysisTables - 未对该类型做任何处理typeName = {typeName}");
                    //中止
                    break;
            }
        }

        /// <summary>
        /// 解析表达式   LastUpdateDate：2022-01-06 16:09:23.916  Author：Lingbug
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        private object GetChildValue(Expression exp)
        {
            //校验
            if (exp == null) return null;
            //类型
            var typeName = exp.GetType().Name;
            switch (typeName)
            {
                case "BinaryExpression":
                case "LogicalBinaryExpression":
                case "MethodBinaryExpression":
                    //类型转换
                    var lExp = exp as BinaryExpression;
                    //递归
                    var ret = GetChildValue(lExp.Left);
                    //返回
                    return IsNullDefaultType(ret) ? GetChildValue(lExp.Right) : ret;
                case "PropertyExpression":
                case "FieldExpression":
                    //类型转换
                    var mberExp = exp as MemberExpression;
                    //返回
                    return GetChildValue(mberExp.Expression);
                case "ConstantExpression":
                    //类型转换
                    var cExp = exp as ConstantExpression;
                    //返回
                    return cExp.Value;
                case "UnaryExpression":
                    //类型转换
                    var unaryExp = exp as UnaryExpression;
                    //返回
                    return GetChildValue(unaryExp.Operand);
                case "InstanceMethodCallExpressionN":
                    //类型转换
                    var imExp = exp as MethodCallExpression;
                    //返回
                    return imExp.Arguments.Count > 0 ? GetChildValue(imExp.Arguments[0]) : null;
                //case "TypedParameterExpression":
                //    //类型转换
                //    var unaryExp = exp as TypedParameterExpression;
                //    //返回
                //    return null;
                default:
                    //打印
                    Log($"GetChildValue - 未对该类型做任何处理typeName = {typeName}");
                    //返回
                    return null;
            }
        }

        /// <summary>
        /// 读取并存储所有参数   LastUpdateDate：2022-01-06 16:15:09.411  Author：Lingbug
        /// </summary>
        /// <param name="paramObj"></param>
        /// <param name="paramList"></param>
        private void AppendParams(object paramObj, Dictionary<string, object> paramList)
        {
            //校验
            if (IsNullDefaultType(paramObj)) return;
            //初始化
            if (paramList == null) paramList = new Dictionary<string, object>();
            //读取属性
            var props = paramObj.GetType().GetProperties();
            foreach (var item in props)
            {
                //读取值
                var value = item.GetValue(paramObj);
                if (IsDefaultType(item.PropertyType))
                {
                    //存储
                    if (value != null) paramList.Add(string.Format("@{0}", item.Name), value);
                    //继续
                    continue;
                }

                //递归
                AppendParams(value, paramList);
            }

            //读取字段
            var fields = paramObj.GetType().GetFields();
            foreach (var item in fields)
            {
                //读取值
                var value = item.GetValue(paramObj);
                if (IsDefaultType(item.FieldType))
                {
                    //存储
                    if (value != null) paramList.Add(string.Format("@{0}", item.Name), value);
                    //继续
                    continue;
                }

                //递归
                AppendParams(item.GetValue(paramObj), paramList);
            }
        }

        /// <summary>
        /// 是否空或者系统默认基本类型   LastUpdateDate：2022-01-06 15:54:10.053  Author：Lingbug
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool IsNullDefaultType(object obj)
        {
            //校验
            if (obj == null) return true;
            //判断
            return IsDefaultType(obj.GetType());
        }

        /// <summary>
        /// 是否是系统默认基本类型   LastUpdateDate：2022-01-06 15:53:55.414  Author：Lingbug
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsDefaultType(Type type)
        {
            //初始化
            var e = new Regex(_defaultBasicTypeName, RegexOptions.IgnoreCase);
            //校验
            return type.Name.ToLower().Contains("nullable") && type.GenericTypeArguments.Length > 0
                ? e.IsMatch(type.GenericTypeArguments[0].Name)
                : e.IsMatch(type.Name);
        }

        /// <summary>
        /// 记录日志   LastUpdateDate：2022-01-06 17:45:40.314  Author：Lingbug
        /// </summary>
        /// <param name="msg"></param>
        private void Log(string msg)
        {
        }

        /// <summary>
        /// 是否是判断是否为空方法   LastUpdateDate：2022-01-07 14:38:12.155  Author：Lingbug
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsNullOrEmptyMethod(MethodInfo method)
        {
            //返回
            return method != null && method.DeclaringType == typeof(string) &&
                   (method.Name == "IsNullOrEmpty" || method.Name == "IsNullOrWhiteSpace");
        }

        private bool IsListContain(MethodInfo method)
        {
            return method != null
                   && (method.DeclaringType == typeof(System.Linq.Enumerable)
                       || (method.DeclaringType.IsGenericType &&
                           method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>))) &&
                   method.Name == "Contains";
        }

        /// <summary>
        /// 是否以xx开头方法   LastUpdateDate：2022-01-07 14:39:40.946  Author：Lingbug
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsStartsWithMethod(MethodInfo method)
        {
            //返回
            return method != null && method.DeclaringType == typeof(string) && method.Name == "StartsWith";
        }

        /// <summary>
        /// 是否以xx结尾方法   LastUpdateDate：2022-01-07 14:39:53.706  Author：Lingbug
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsEndsWithMethod(MethodInfo method)
        {
            //返回
            return method != null && method.DeclaringType == typeof(string) && method.Name == "EndsWith";
        }

        /// <summary>
        /// 是否是包含xx方法   LastUpdateDate：2022-01-07 14:43:26.457  Author：Lingbug
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private bool IsContainsMethod(MethodInfo method)
        {
            //返回
            return method != null && method.DeclaringType == typeof(string) && method.Name == "Contains";
        }

        //public Dictionary<string, object> GetParams(object paramObj)
        //{
        //    Dictionary<string, object> dicParams = new Dictionary<string, object>();
        //    AppendParams(paramObj, dicParams);
        //    return dicParams;
        //}
    }

    public class AnalysisData
    {
        public Dictionary<string, AnalysisTable> TableList { get; set; }

        public List<string> StackList { get; set; }

        public Dictionary<string, object> ParamList { get; set; }
    }

    public class AnalysisTable
    {
        public string Name { get; set; }

        public Type TableType { get; set; }

        public bool IsMainTable { get; set; }
    }
}