using System.CodeDom;
using System.Text;

namespace Avro.codegen
{
    public class ETPCodeGen : CodeGen
    {
        protected override CodeTypeDeclaration processRecord(Schema schema)
        {
            RecordSchema recordSchema = schema as RecordSchema;
            if (null == recordSchema) throw new CodeGenException("Unable to cast schema into a record");

            bool isError = recordSchema.Tag == Schema.Type.Error;

            // declare the class
            var ctd = new CodeTypeDeclaration(CodeGenUtil.Instance.Mangle(recordSchema.Name));
            //ctd.BaseTypes.Add(isError ? "SpecificException" : "ISpecificRecord");

            ctd.Attributes = MemberAttributes.Public;
            ctd.IsClass = true;

            foreach (Field field in recordSchema.Fields)
            {
                // Determine type of field
                bool nullibleEnum = false;
                string baseType = getType(field.Schema, false, ref nullibleEnum);
                var ctrfield = new CodeTypeReference(baseType);

                var mangledName = CodeGenUtil.Instance.Mangle(field.Name);
                CodeSnippetTypeMember snippet = new CodeSnippetTypeMember();

                StringBuilder propertyBuilder = new StringBuilder();
                if (field.Schema.Tag == Schema.Type.Union)
                {
                    var unionSchemas = field.Schema as UnionSchema;

                    var unionAttributes = new CodeAttributeArgument[unionSchemas.Schemas.Count];
                    
                    propertyBuilder.Append("        [AvroUnion(");
                    for (int i = 0; i < unionSchemas.Count; i++)
                    {
                        var something = unionSchemas.Schemas[i];

                        var typeName = unionSchemas.Schemas[i].Name;

                        if (typeName == "null")
                        {
                            typeName = "AvroNull";
                        }
                        else if (typeName == "bytes")
                        {
                            typeName = "byte[]";
                        }
                        else if (typeName == "boolean")
                        {
                            typeName = "bool";
                        }

                        if (something.Tag == Schema.Type.Record)
                        {
                            var rschema = something as RecordSchema;

                            if (rschema.Namespace != recordSchema.Namespace)
                                typeName = rschema.Fullname;
                        }

                        propertyBuilder.Append(string.Format("typeof({0})", typeName));

                        if (i < unionSchemas.Count - 1)
                        {
                            propertyBuilder.Append(", ");
                        }
                        //var snpt = new CodeSnippetExpression(string.Format("typeof({0})", typeName));
                    }
                    propertyBuilder.AppendLine(")]");
                }

                //snippet.Comments.Add(new CodeCommentStatement("this is integer property", true));
                propertyBuilder.AppendLine("        public " + baseType + " " + mangledName.FirstCharToUpper() + "{ get; set; }\n");
                snippet.Text = propertyBuilder.ToString();
                //snippet.Text = "        public "+ baseType + " " + mangledName.FirstCharToUpper()  + "{ get; set; }\n";
                //snippet.Text = string.Format("public {0} {1} { get; set; }", baseType, mangledName.FirstCharToUpper());

                ctd.Members.Add(snippet);
               
            }
            
            string nspace = recordSchema.Namespace;
            if (string.IsNullOrEmpty(nspace))
                throw new CodeGenException("Namespace required for record schema " + recordSchema.Name);
            CodeNamespace codens = addNamespace(nspace);

            codens.Types.Add(ctd);

            return ctd;
        }
    }
}
