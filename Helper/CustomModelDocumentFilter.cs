using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Utility;

namespace PecBMS.Helper
{
    /// <summary>The first class...</summary>
    public class MyClassTest
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServiceStatus ServiceStatus { get; set; }

    }

    /// <summary>
    /// For properties that are Dictionary[SomeEnum, valueType] alter the schema
    /// so the generated SDK code will be IDictionary[string, valueType].
    /// </summary>
    public class EnumDictionaryToStringDictionarySchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// Apply the schema changes
        /// </summary>
        /// <param name="schema">The schema model</param>
        /// <param name="context">The schema filter context</param>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // Only for fields that are Dictionary<Enum, TValue>
            //
            if (!context.Type.IsGenericType)
                return;

            if (!context.Type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>))
                && !context.Type.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDictionary<,>)))
                return;

            var keyType = context.Type.GetGenericArguments()[0];

            if (!keyType.IsEnum)
                return;

            var valueType = context.Type.GetGenericArguments()[1];
            var valueTypeSchema = context.SchemaGenerator.GenerateSchema(valueType, context.SchemaRepository);

            schema.Type = "object";
            schema.Properties.Clear();
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = valueTypeSchema;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SwaggerDocumentFilter<T> : IDocumentFilter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="openapiDoc"></param>
        /// <param name="context"></param>
        public void Apply(OpenApiDocument openapiDoc, DocumentFilterContext context)
        {
            var DocumentNames = typeof(T).GetCustomAttribute<ApiExplorerSettingsAttribute>();
            if (DocumentNames == null || !DocumentNames.GroupName.Any() || context.DocumentName == DocumentNames.GroupName)
            {
                context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
            }
        }
    }

    public class EnumTypesDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (var path in swaggerDoc.Paths.Values)
            {
                foreach (var operation in path.Operations.Values)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        var schemaReferenceId = parameter.Schema.Reference?.Id;

                        if (string.IsNullOrEmpty(schemaReferenceId)) continue;

                        var schema = context.SchemaRepository.Schemas[schemaReferenceId];

                        if (schema.Enum == null || schema.Enum.Count == 0) continue;

                        parameter.Description += "<p>Variants:</p>";

                        int cutStart = schema.Description.IndexOf("<ul>");

                        int cutEnd = schema.Description.IndexOf("</ul>") + 5;

                        parameter.Description += schema.Description
                            .Substring(cutStart, cutEnd - cutStart);
                    }
                }
            }
        }
    }


    public class EnumTypesSchemaFilter : ISchemaFilter
    {
        private readonly XDocument _xmlComments;

        public EnumTypesSchemaFilter(string xmlPath)
        {
            if (File.Exists(xmlPath))
            {
                _xmlComments = XDocument.Load(xmlPath);
            }
        }

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (_xmlComments == null) return;

            if (schema.Enum != null && schema.Enum.Count > 0 &&
                context.Type != null && context.Type.IsEnum)
            {
                schema.Description += "<p>Members:</p><ul>";

                var fullTypeName = context.Type.FullName;

                foreach (var enumMemberName in schema.Enum.OfType<OpenApiString>().
                         Select(v => v.Value))
                {
                    var fullEnumMemberName = $"F:{fullTypeName}.{enumMemberName}";

                    var enumMemberComments = _xmlComments.Descendants("member")
                        .FirstOrDefault(m => m.Attribute("name").Value.Equals
                        (fullEnumMemberName, StringComparison.OrdinalIgnoreCase));

                    if (enumMemberComments == null) continue;

                    var summary = enumMemberComments.Descendants("summary").FirstOrDefault();

                    if (summary == null) continue;

                    schema.Description += $"<li><i>{enumMemberName}</i> -{summary.Value.Trim()}</ li > ";
                }

                schema.Description += "</ul>";
            }
        }
    }

}


