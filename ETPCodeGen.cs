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
            ctd.BaseTypes.Add(isError ? "SpecificException" : "ISpecificRecord");

            ctd.Attributes = MemberAttributes.Public;
            ctd.IsClass = true;
            ctd.IsPartial = true;

            createSchemaField(schema, ctd, isError);

            // declare Get() to be used by the Writer classes
            var cmmGet = new CodeMemberMethod();
            cmmGet.Name = "Get";
            cmmGet.Attributes = MemberAttributes.Public;
            cmmGet.ReturnType = new CodeTypeReference("System.Object");
            cmmGet.Parameters.Add(new CodeParameterDeclarationExpression(typeof(int), "fieldPos"));
            StringBuilder getFieldStmt = new StringBuilder("switch (fieldPos)\n\t\t\t{\n");

            // declare Put() to be used by the Reader classes
            var cmmPut = new CodeMemberMethod();
            cmmPut.Name = "Put";
            cmmPut.Attributes = MemberAttributes.Public;
            cmmPut.ReturnType = new CodeTypeReference(typeof(void));
            cmmPut.Parameters.Add(new CodeParameterDeclarationExpression(typeof(int), "fieldPos"));
            cmmPut.Parameters.Add(new CodeParameterDeclarationExpression("System.Object", "fieldValue"));
            var putFieldStmt = new StringBuilder("switch (fieldPos)\n\t\t\t{\n");

            if (isError)
            {
                cmmGet.Attributes |= MemberAttributes.Override;
                cmmPut.Attributes |= MemberAttributes.Override;
            }

            foreach (Field field in recordSchema.Fields)
            {
                // Determine type of field
                bool nullibleEnum = false;
                string baseType = getType(field.Schema, false, ref nullibleEnum);
                var ctrfield = new CodeTypeReference(baseType);

                // Create field
                string privFieldName = string.Concat("_", field.Name);
                var codeField = new CodeMemberField(ctrfield, privFieldName);
                codeField.Attributes = MemberAttributes.Private;

                // Process field documentation if it exist and add to the field
                CodeCommentStatement propertyComment = null;
                if (!string.IsNullOrEmpty(field.Documentation))
                {
                    propertyComment = createDocComment(field.Documentation);
                    if (null != propertyComment)
                        codeField.Comments.Add(propertyComment);
                }

                // Add field to class
                ctd.Members.Add(codeField);

                // Create reference to the field - this.fieldname
                var fieldRef = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), privFieldName);
                var mangledName = CodeGenUtil.Instance.Mangle(field.Name);

                // Create field property with get and set methods
                var property = new CodeMemberProperty();
                property.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                property.Name = mangledName.FirstCharToUpper();
                property.Type = ctrfield;
                property.GetStatements.Add(new CodeMethodReturnStatement(fieldRef));
                property.SetStatements.Add(new CodeAssignStatement(fieldRef, new CodePropertySetValueReferenceExpression()));
                if (null != propertyComment)
                    property.Comments.Add(propertyComment);

                // Add field property to class
                ctd.Members.Add(property);

                // add to Get()
                getFieldStmt.Append("\t\t\tcase ");
                getFieldStmt.Append(field.Pos);
                getFieldStmt.Append(": return this.");
                getFieldStmt.Append(privFieldName);
                getFieldStmt.Append(";\n");

                // add to Put()
                putFieldStmt.Append("\t\t\tcase ");
                putFieldStmt.Append(field.Pos);
                putFieldStmt.Append(": this.");
                putFieldStmt.Append(privFieldName);

                if (nullibleEnum)
                {
                    putFieldStmt.Append(" = fieldValue == null ? (");
                    putFieldStmt.Append(baseType);
                    putFieldStmt.Append(")null : (");

                    string type = baseType.Remove(0, 16);  // remove System.Nullable<
                    type = type.Remove(type.Length - 1);   // remove >

                    putFieldStmt.Append(type);
                    putFieldStmt.Append(")fieldValue; break;\n");
                }
                else
                {
                    putFieldStmt.Append(" = (");
                    putFieldStmt.Append(baseType);
                    putFieldStmt.Append(")fieldValue; break;\n");
                }
            }

            // end switch block for Get()
            getFieldStmt.Append("\t\t\tdefault: throw new AvroRuntimeException(\"Bad index \" + fieldPos + \" in Get()\");\n\t\t\t}");
            var cseGet = new CodeSnippetExpression(getFieldStmt.ToString());
            cmmGet.Statements.Add(cseGet);
            ctd.Members.Add(cmmGet);

            // end switch block for Put()
            putFieldStmt.Append("\t\t\tdefault: throw new AvroRuntimeException(\"Bad index \" + fieldPos + \" in Put()\");\n\t\t\t}");
            var csePut = new CodeSnippetExpression(putFieldStmt.ToString());
            cmmPut.Statements.Add(csePut);
            ctd.Members.Add(cmmPut);

            string nspace = recordSchema.Namespace;
            if (string.IsNullOrEmpty(nspace))
                throw new CodeGenException("Namespace required for record schema " + recordSchema.Name);
            CodeNamespace codens = addNamespace(nspace);

            codens.Types.Add(ctd);

            return ctd;
        }
    }
}
