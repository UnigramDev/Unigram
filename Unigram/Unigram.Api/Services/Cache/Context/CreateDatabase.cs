using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Universal.WinSQLite;

namespace Telegram.Api.Services.Cache.Context
{
    public static class CreateDatabase
    {
        private static string _path;
        private static Database _database;

        public static void Open(out Database database)
        {
            if (_database == null)
            {
                _path = FileUtils.GetFileName("database.sqlite");
                Sqlite3.sqlite3_open_v2(_path, out _database, 2 | 4, string.Empty);
            }

            database = _database;
        }

        public static void Execute(Database database)
        {
            if (SettingsHelper.DatabaseVersion < 6)
            {
                Execute(database, "DROPTABLE IF EXISTS `Chats`");
            }

            Execute(database,
@"CREATE TABLE IF NOT EXISTS `Users` (
	`id`	BIGINT NOT NULL,
	`access_hash`	BIGINT,
	`flags`	INTEGER NOT NULL,
	`first_name`	TEXT,
	`last_name`	TEXT,
	`phone`	TEXT,
	`username`	TEXT,
	`restriction_reason`	TEXT,
	`bot_info_version`	INTEGER,
	`bot_inline_placeholder`	TEXT,
	`photo_id`	BIGINT,
	`photo_small_local_id`	INTEGER,
	`photo_small_secret`	BIGINT,
	`photo_small_volume_id`	BIGINT,
	`photo_small_dc_id`	INTEGER,
	`photo_big_local_id`	INTEGER,
	`photo_big_secret`	BIGINT,
	`photo_big_volume_id`	BIGINT,
	`photo_big_dc_id`	INTEGER,
	`status`	INTEGER,
	`status_was_online`	INTEGER,
	`status_expires`	INTEGER,
	PRIMARY KEY(`id`)
);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Users.username_index` ON `Users` (`username`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Users.phone_index` ON `Users` (`phone`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Users.last_name_index` ON `Users` (`last_name`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Users.first_name_index` ON `Users` (`first_name`);");

            Execute(database,
@"CREATE TABLE `Chats` (
	`id`	BIGINT NOT NULL,
	`access_hash`	BIGINT,
	`flags`	INTEGER NOT NULL,
	`title`	TEXT,
	`username`	TEXT,
	`version`	INTEGER NOT NULL,
	`participants_count`	INTEGER,
	`date`	INTEGER NOT NULL,
	`restriction_reason`	TEXT,
	`photo_small_local_id`	INTEGER,
	`photo_small_secret`	INTEGER,
	`photo_small_volume_id`	BIGINT,
	`photo_small_dc_id`	INTEGER,
	`photo_big_local_id`	INTEGER,
	`photo_big_secret`	BIGINT,
	`photo_big_volume_id`	BIGINT,
	`photo_big_dc_id`	INTEGER,
	`migrated_to_id`	INTEGER,
	`migrated_to_access_hash`	INTEGER
    `admin_rights` INTEGER,
    `banned_rights` INTEGER,
    PRIMARY KEY(`id`)
);");
            Execute(database, "CREATE INDEX `Chats.title_index` ON `Chats` (`title`);");
            Execute(database, "CREATE INDEX `Chats.username_index` ON `Chats` (`username`);");
            Execute(database, "CREATE INDEX `Chats.migrated_to_id_index` ON `Chats` (`migrated_to_id`);");
            Execute(database, "CREATE INDEX `Chats.id_index` ON `Chats` (`id`);");

            var version = SettingsHelper.DatabaseVersion;
            if (version < 6)
            {
                Execute(database, "ALTER TABLE `Chats` ADD COLUMN `admin_rights` INTEGER");
                Execute(database, "ALTER TABLE `Chats` ADD COLUMN `banned_rights` INTEGER");

                version = 6;
            }

            SettingsHelper.DatabaseVersion = version;
        }

        private static void Execute(Database database, string query)
        {
            Statement statement;
            Sqlite3.sqlite3_prepare_v2(database, query, out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }
    }
}
