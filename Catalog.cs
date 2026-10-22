using System;
using System.Collections.Generic;
using System.Linq;
using DungeonAlchemist;
using DungeonAlchemist.Config;
using DungeonAlchemist.Database;

namespace MapGenerator
{
    internal sealed class ThemeChoice
    {
        public string Id;
        public string Name;
    }

    internal sealed class TokenChoice
    {
        public string Id;
        public string Name;
        public string Group;
    }

    internal sealed class Catalog
    {
        public List<ThemeChoice> Themes = new List<ThemeChoice>();
        public List<TokenChoice> Tokens = new List<TokenChoice>();

        public static Catalog Current { get; private set; }

        public static void Refresh(DatabaseConnection db)
        {
            var catalog = new Catalog();
            if (db == null)
            {
                Current = catalog;
                return;
            }

            foreach (var theme in db.ThemeConfigs.List() ?? new List<ThemeConfig>())
            {
                if (theme == null || !theme.visible || !theme.IsAvailable() || theme.roomConfigs == null)
                    continue;
                if (!theme.roomConfigs.Any(room => room != null && room.normalRoom))
                    continue;
                catalog.Themes.Add(new ThemeChoice
                {
                    Id = theme.Id,
                    Name = string.IsNullOrEmpty(theme.name) ? theme.Id : theme.name,
                });
            }
            catalog.Themes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var cfg in db.BuildingBlockConfigs.ListNonDeprecated())
            {
                if (cfg == null || cfg.deprecated || cfg.automaticType != AutomaticBuildingBlockType.None)
                    continue;
                if (cfg.type != BuildingBlockType.Object || !cfg.IsToken())
                    continue;
                if (db.IsImportedModelConfig(cfg.Id) && !db.IsBlockVisible(cfg.Id))
                    continue;

                string group = "Other";
                var categories = cfg.GetNormalDisplayCategories(db);
                if (categories != null)
                {
                    foreach (var categoryId in categories)
                    {
                        var category = db.DisplayCategories.GetOrNull(categoryId);
                        if (category != null && !string.IsNullOrEmpty(category.name))
                        {
                            group = category.name;
                            break;
                        }
                    }
                }

                catalog.Tokens.Add(new TokenChoice
                {
                    Id = cfg.Id,
                    Name = string.IsNullOrEmpty(cfg.name) ? cfg.Id : cfg.name,
                    Group = group,
                });
            }
            catalog.Tokens.Sort((a, b) =>
            {
                int group = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
                return group != 0 ? group : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Current = catalog;
        }
    }
}
