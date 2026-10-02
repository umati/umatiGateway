"""TBD Module Description"""

import os
import re


def rename_directories(root_dir):
    """
    Renames subdirectories in the given root directory.

    Args:
        root_dir (str): Path to the root directory.
    """
    # topdown=False ist wichtig für sicheres Umbenennen
    for dirpath, dirnames, _ in os.walk(root_dir, topdown=False):
        for dirname in dirnames:
            match = re.match(r"^(\d+)_+(.*)", dirname)
            if match:
                old_path = os.path.join(dirpath, dirname)
                new_name = match.group(2)
                new_path = os.path.join(dirpath, new_name)

                if not os.path.exists(new_path):
                    print(f"Renaming: {old_path} -> {new_path}")
                    os.rename(old_path, new_path)
                else:
                    print(
                        f"Konflikt: {new_path} existiert bereits – Überspringe {old_path}"
                    )


if __name__ == "__main__":
    # Hier den Pfad zum Zielverzeichnis eintragen
    ZIEL_VERZEICHNIS = "."
    rename_directories(ZIEL_VERZEICHNIS)
