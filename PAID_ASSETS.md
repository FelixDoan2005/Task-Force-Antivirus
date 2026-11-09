# Paid assets

These packs are licensed and are not in this repo, so don't commit them. `.gitignore` already blocks them.

To open the project with everything working, get your own licensed copy of each pack and import it into `Assets/`:

| Pack | Where it goes | Used for |
|---|---|---|
| Elf Guardian Knight Modular (Novozhilov3D) | `Assets/Novozhilov3D/` | Soldier models |
| Virus (Unity Asset Store, product ID 274862) | `Assets/Virus/` | Virus models, materials and animations |

Without them the project still opens, but soldiers and viruses show missing models and pink materials.

## Files to generate after importing

These are built from the packs, so they aren't in the repo either:

- **Merged elf meshes** (`Assets/_Project/Art/Characters/ElfSoldiers/ElfKnight_*.asset`): run **Tools > Squad > Build Elf Soldiers** in Unity.
- **Baked virus meshes** (`Assets/_Project/Meshes/VirusBaked/`): there's no menu item for these yet, so ask the project owner.
