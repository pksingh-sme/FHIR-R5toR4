using System;
using System.IO;
using Newtonsoft.Json.Linq;

class FhirR5ToR4Liquid
{
    public static string GenerateR4Liquid(JObject diffJson, string resourceName)
    {
        if (!diffJson.ContainsKey(resourceName))
            return "{% mergeDiff msg -%}\n{% endmergeDiff %}\n";

        JObject elements = (JObject)diffJson[resourceName];

        string liquid = "{% mergeDiff msg -%}\n";
        liquid += $"{{% if msg.resourceType == \"{resourceName}\" -%}}\n";
        liquid += "  {% assign fields = \"\" | split: \"\" %}\n";

        // Participant.actor -> recorder + asserter
        foreach (var path in elements.Properties())
        {   
            if (path.Name.EndsWith("participant.actor"))
            {
                liquid += "  {% if msg.participant.actor -%}\n";
                liquid += "    {% assign fields = fields | push: '\"recorder\" : {{msg.participant.actor}}' %}\n";
                liquid += "    {% assign fields = fields | push: '\"asserter\" : {{msg.participant.actor}}' %}\n";
                liquid += "  {% endif -%}\n";
                break; // only once
            }
        }

        // Evidence -> code + detail
        foreach (var path in elements.Properties())
        {
            if (path.Name.StartsWith(resourceName + ".evidence"))
            {
                liquid += "  {% if msg.evidence -%}\n";
                liquid += "    {% assign fields = fields | push: '\"evidence.code\" : {{msg.evidence}}' %}\n";
                liquid += "    {% assign fields = fields | push: '\"evidence.detail\" : {{msg.evidence}}' %}\n";
                liquid += "  {% endif -%}\n";
                break;
            }
        }

        // ClinicalStatus -> first
        foreach (var path in elements.Properties())
        {
            if (path.Name.EndsWith("clinicalStatus"))
            {
                liquid += "  {% if msg.clinicalStatus -%}\n";
                liquid += "    {% assign fields = fields | push: '\"clinicalStatus\" : {{msg.clinicalStatus | first}}' %}\n";
                liquid += "  {% endif -%}\n";
                break;
            }
        }

        // Category -> first
        foreach (var path in elements.Properties())
        {
            if (path.Name.EndsWith("category"))
            {
                liquid += "  {% if msg.category -%}\n";
                liquid += "    {% assign fields = fields | push: '\"category\" : {{msg.category | first}}' %}\n";
                liquid += "  {% endif -%}\n";
                break;
            }
        }

        liquid += "  {{ fields | join: \",\" }}\n";
        liquid += "{% endif %}\n{% endmergeDiff %}\n";

        return liquid;
    }

    static void Main(string[] args)
    {
        try
        {
            // 1. Specify the path to your JSON file
            string jsonFilePath = "../fhir_r5_types_diff.json";

            // 2. Read JSON content from the file
            string jsonContent = File.ReadAllText(jsonFilePath);

            // 3. Parse JSON
            JObject diffJson = JObject.Parse(jsonContent);

            // 4. Generate Liquid template for "Condition"
            // string liquidOutput = GenerateR4Liquid(diffJson, "Condition");
            string liquidOutput = GenerateR4Liquid(diffJson, "EvidenceVariable");


            // 5. Output to console
            Console.WriteLine(liquidOutput);

            // 6. Optionally save to file
            File.WriteAllText("Condition_Liquid_R4.liquid", liquidOutput);

            Console.WriteLine("\nLiquid template saved to Condition_Liquid_R4.liquid");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading JSON or generating Liquid template:");
            Console.WriteLine(ex.Message);
        }
    }
}
