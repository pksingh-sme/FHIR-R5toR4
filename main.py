import json
from pathlib import Path
from datetime import datetime

def generate_r4_liquid_generic(diff_json, resource_name):
    """
    Generate a backward-compatible R4 Liquid template for a given FHIR resource.
    Handles:
      - Ignoring Added Elements
      - Restoring Deleted / Moved elements
      - Nested paths dynamically
    """
    if resource_name not in diff_json:
        return "{% mergeDiff msg -%}\n{% endmergeDiff %}\n"

    elements = diff_json[resource_name]
    liquid = "{% mergeDiff msg -%}\n"
    liquid += f'{{% if msg.resourceType == "{resource_name}" -%}}\n'
    liquid += '  {% assign fields = "" | split: "" %}\n'

    restored_fields = set()

    for path, changes in elements.items():
        # Clean field path
        field_path = path.replace(resource_name + ".", "")

        # Skip Added Elements
        if any("Added Element" in c or "Added Mandatory Element" in c for c in changes):
            continue

        # Handle Deleted (-> X)
        for change in changes:
            if "Deleted (->" in change:
                target = change.split("->")[1].strip(" )")
                if field_path not in restored_fields:
                    liquid += f'  {{% assign fields = fields | push: \'"{field_path}" : {{msg.{target}}}\' %}}\n'
                    restored_fields.add(field_path)

        # Handle plain Deleted
        if any("Deleted" in c and "(->" not in c for c in changes):
            if field_path not in restored_fields:
                liquid += f'  {{% if msg.{field_path} == null -%}}\n'
                liquid += f'    {{% assign fields = fields | push: \'"{field_path}" : null\' %}}\n'
                liquid += '  {% endif -%}\n'
                restored_fields.add(field_path)

    liquid += '  {{ fields | join: "," }}\n'
    liquid += '{% endif %}\n{% endmergeDiff %}\n'

    return liquid


if __name__ == "__main__":
    # Path to your JSON file
    json_file_path = Path("fhir_r5_types_diff.json")
    if not json_file_path.exists():
        print(f"JSON file not found: {json_file_path}")
        exit(1)

    # Load JSON
    with open(json_file_path, "r", encoding="utf-8") as f:
        diff_json = json.load(f)

    # Example: generate for any resource
    resource_name = "Evidence"  # can be "Condition", "Observation", etc.
    liquid_output = generate_r4_liquid_generic(diff_json, resource_name)

    # Output to console
    print(liquid_output)

    # Save to file
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"{resource_name}_Liquid_R4_{timestamp}.liquid"
    with open(output_file, "w", encoding="utf-8") as f:
        f.write(liquid_output)
    print(f"\nLiquid template saved to {output_file}")
