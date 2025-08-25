import json
from pathlib import Path
from datetime import datetime

# -----------------------------
# Helper Functions
# -----------------------------

def is_added_element(changes):
    return any("Added Element" in c or "Added Mandatory Element" in c for c in changes)

def is_deleted_mapping(change):
    return "Deleted (->" in change

def extract_deleted_target(change):
    return change.split("->")[1].strip(" )")

def generate_liquid_line(field_name, msg_ref):
    """Return Liquid push line for a field."""
    return f'    {{% assign fields = fields | push: \'"{field_name}" : {{{{ {msg_ref} }}}}\' %}}\n'

def handle_code_changes(field_path, changes, added_fields, liquid_lines):
    """
    Handle Remove/Add codes for backward-compatible FHIR Liquid.
    Remove codes -> restore in R4
    Add codes -> ignore
    """
    for change in changes:
        if "Remove codes" in change:
            codes_part = change.replace("Remove codes", "").strip()
            codes = [c.strip() for c in codes_part.split(",") if c.strip()]
            for code in codes:
                key = f"{field_path}.{code}"
                if key not in added_fields:
                    liquid_lines.append(f'  {{% if msg.{field_path} -%}}\n')
                    liquid_lines.append(f'    {{% assign fields = fields | push: \'"{field_path}" : "{code}"\' %}}\n')
                    liquid_lines.append("  {% endif -%}\n")
                    added_fields.add(key)

# -----------------------------
# Main Generic Liquid Generator
# -----------------------------

def generate_r4_liquid_generic(diff_json, resource_name):
    """
    Fully generic function to handle any FHIR resource and keys.
    """
    if resource_name not in diff_json:
        return "{% mergeDiff msg -%}\n{% endmergeDiff %}\n"

    elements = diff_json[resource_name]
    liquid_lines = [
        "{% mergeDiff msg -%}\n",
        f'{{% if msg.resourceType == "{resource_name}" -%}}\n',
        '  {% assign fields = "" | split: "" %}\n'
    ]

    added_fields = set()

    for path, changes in elements.items():
        field_path = path.replace(resource_name + ".", "")

        if is_added_element(changes):
            continue

        # Handle Remove/Add codes
        if any("codes" in c for c in changes):
            handle_code_changes(field_path, changes, added_fields, liquid_lines)
            continue

        # Deleted mapping
        for change in changes:
            if is_deleted_mapping(change):
                target = extract_deleted_target(change)
                target_field = target.replace(resource_name + ".", "")
                if field_path not in added_fields:
                    liquid_lines.append(generate_liquid_line(field_path, f"msg.{target_field}"))
                    added_fields.add(field_path)

        # Plain Deleted (restore null)
        if any("Deleted" in c and "(->" not in c for c in changes):
            if field_path not in added_fields:
                liquid_lines.append(f'  {{% if msg.{field_path} == null -%}}\n')
                liquid_lines.append(f'    {{% assign fields = fields | push: \'"{field_path}" : null\' %}}\n')
                liquid_lines.append("  {% endif -%}\n")
                added_fields.add(field_path)

        # Default push for all remaining keys
        if field_path not in added_fields:
            liquid_lines.append(f'  {{% if msg.{field_path} -%}}\n')
            liquid_lines.append(generate_liquid_line(field_path, f"msg.{field_path}"))
            liquid_lines.append("  {% endif -%}\n")
            added_fields.add(field_path)

    liquid_lines.append('  {{ fields | join: "," }}\n')
    liquid_lines.append("{% endif %}\n{% endmergeDiff %}\n")
    return "".join(liquid_lines)

# -----------------------------
# File Utilities
# -----------------------------

def load_json(file_path):
    path = Path(file_path)
    if not path.exists():
        raise FileNotFoundError(f"JSON file not found: {file_path}")
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def save_liquid_to_file(resource_name, liquid_text):
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    file_name = f"{resource_name}_Liquid_R4_{timestamp}.liquid"
    with open(file_name, "w", encoding="utf-8") as f:
        f.write(liquid_text)
    print(f"\nLiquid template saved to {file_name}")

# -----------------------------
# Example Usage
# -----------------------------

if __name__ == "__main__":
    diff_json = load_json("fhir_r5_types_diff.json")

    # Loop over all resources
    # You can change resource dynamically
    resource_name = "ChargeItemDefinition" #CompartmentDefinition , "ChargeItemDefinition", "Condition"
    liquid_output = generate_r4_liquid_generic(diff_json, resource_name)
    print(liquid_output)
    save_liquid_to_file(resource_name, liquid_output)
