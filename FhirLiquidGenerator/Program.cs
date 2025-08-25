using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

class FhirR5ToR4Liquid
{
    // -----------------------------
    // Helper Methods
    // -----------------------------
    static bool IsAddedElement(JArray changes)
    {
        foreach (var c in changes)
        {
            string val = c.ToString();
            if (val.Contains("Added Element") || val.Contains("Added Mandatory Element"))
                return true;
        }
        return false;
    }

    static bool IsDeletedMapping(string change)
    {
        return change.Contains("Deleted (->");
    }

    static string ExtractDeletedTarget(string change)
    {
        var parts = change.Split("->");
        if (parts.Length > 1)
            return parts[1].Trim(' ', ')');
        return "";
    }

    static string GenerateLiquidLine(string fieldName, string msgRef)
    {
        return $"    {{% assign fields = fields | push: '\"{fieldName}\" : {{{{ {msgRef} }}}}' %}}\n";
    }

    static bool HandleSpecialCases(string fieldPath, HashSet<string> addedFields, List<string> liquidLines)
    {
        // participant.actor -> recorder + asserter
        if (fieldPath.EndsWith("participant.actor"))
        {
            if (!addedFields.Contains("recorder"))
            {
                liquidLines.Add("  {% if msg.participant.actor -%}\n");
                liquidLines.Add(GenerateLiquidLine("recorder", "msg.participant.actor"));
                liquidLines.Add(GenerateLiquidLine("asserter", "msg.participant.actor"));
                liquidLines.Add("  {% endif -%}\n");
                addedFields.Add("recorder");
                addedFields.Add("asserter");
            }
            return true;
        }
        // evidence -> code + detail
        else if (fieldPath.StartsWith("evidence"))
        {
            foreach (var f in new[] { "evidence.code", "evidence.detail" })
            {
                if (!addedFields.Contains(f))
                {
                    liquidLines.Add("  {% if msg.evidence -%}\n");
                    liquidLines.Add(GenerateLiquidLine(f, "msg.evidence"));
                    liquidLines.Add("  {% endif -%}\n");
                    addedFields.Add(f);
                }
            }
            return true;
        }
        return false;
    }

    // -----------------------------
    // Main Liquid Generator
    // -----------------------------
    static string GenerateR4LiquidNoDuplicates(JObject diffJson, string resourceName)
    {
        if (!diffJson.ContainsKey(resourceName))
            return "{% mergeDiff msg -%}\n{% endmergeDiff %}\n";

        JObject elements = (JObject)diffJson[resourceName];
        List<string> liquidLines = new List<string>
        {
            "{% mergeDiff msg -%}\n",
            $"{{% if msg.resourceType == \"{resourceName}\" -%}}\n",
            "  {% assign fields = \"\" | split: \"\" %}\n"
        };

        HashSet<string> addedFields = new HashSet<string>();

        foreach (var prop in elements.Properties())
        {
            string path = prop.Name;
            JArray changes = (JArray)prop.Value;
            string fieldPath = path.Replace(resourceName + ".", "");

            if (IsAddedElement(changes))
                continue;

            // Special cases
            if (HandleSpecialCases(fieldPath, addedFields, liquidLines))
                continue;

            // Deleted mapping
            foreach (var c in changes)
            {
                string change = c.ToString();
                if (IsDeletedMapping(change))
                {
                    string target = ExtractDeletedTarget(change);
                    string targetField = target.Replace(resourceName + ".", "");
                    if (!addedFields.Contains(fieldPath))
                    {
                        liquidLines.Add(GenerateLiquidLine(fieldPath, $"msg.{targetField}"));
                        addedFields.Add(fieldPath);
                    }
                }
            }

            // Plain deleted
            foreach (var c in changes)
            {
                string change = c.ToString();
                if (change.Contains("Deleted") && !change.Contains("(->") && !addedFields.Contains(fieldPath))
                {
                    liquidLines.Add($"  {{% if msg.{fieldPath} == null -%}}\n");
                    liquidLines.Add($"    {{% assign fields = fields | push: '\"{fieldPath}\" : null' %}}\n");
                    liquidLines.Add("  {% endif -%}\n");
                    addedFields.Add(fieldPath);
                }
            }

            // Default push
            if (!addedFields.Contains(fieldPath))
            {
                liquidLines.Add($"  {{% if msg.{fieldPath} -%}}\n");
                liquidLines.Add(GenerateLiquidLine(fieldPath, $"msg.{fieldPath}"));
                liquidLines.Add("  {% endif -%}\n");
                addedFields.Add(fieldPath);
            }
        }

        liquidLines.Add("  {{ fields | join: \",\" }}\n");
        liquidLines.Add("{% endif %}\n{% endmergeDiff %}\n");

        return string.Join("", liquidLines);
    }

    // -----------------------------
    // File Utilities
    // -----------------------------
    static JObject LoadJson(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"JSON file not found: {filePath}");
        string content = File.ReadAllText(filePath);
        return JObject.Parse(content);
    }

    static string SaveLiquidToFile(string resourceName, string liquidText)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{resourceName}_Liquid_R4_{timestamp}.liquid";
        File.WriteAllText(fileName, liquidText);
        return fileName;
    }

    // -----------------------------
    // Main
    // -----------------------------
    static void Main(string[] args)
    {
        try
        {
            string jsonFilePath = "fhir_r5_types_diff.json"; // change path if needed
            JObject diffJson = LoadJson(jsonFilePath);

            string resourceName = "Condition"; // or "Evidence", etc.
            string liquidOutput = GenerateR4LiquidNoDuplicates(diffJson, resourceName);

            Console.WriteLine(liquidOutput);

            string savedFile = SaveLiquidToFile(resourceName, liquidOutput);
            Console.WriteLine($"\nLiquid template saved to {savedFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
