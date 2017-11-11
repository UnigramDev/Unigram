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
            if (SettingsHelper.DatabaseVersion < 8)
            {
                Execute(database, "DROP TABLE IF EXISTS `Chats`");
                Execute(database, "DROP TABLE IF EXISTS `Dialogs`");
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
    `index` INTEGER NOT NULL,
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
	`migrated_to_access_hash`	INTEGER,
    `admin_rights` INTEGER,
    `banned_rights` INTEGER,
    PRIMARY KEY(`id`)
);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Chats.title_index` ON `Chats` (`title`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Chats.username_index` ON `Chats` (`username`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Chats.migrated_to_id_index` ON `Chats` (`migrated_to_id`);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Chats.id_index` ON `Chats` (`id`);");

            Execute(database,
@"CREATE TABLE `Dialogs` (
	`flags`	INTEGER NOT NULL,
	`peer`	BIGINT NOT NULL,
    `index` INTEGER NOT NULL,
	`top_message`	INTEGER NOT NULL,
	`read_inbox_max_id`	INTEGER NOT NULL,
	`read_outbox_max_id`	INTEGER NOT NULL,
	`unread_count`	INTEGER NOT NULL,
	`notify_settings_flags`	INTEGER,
	`notify_settings_mute_until`	INTEGER,
	`notify_settings_sound`	TEXT,
	`pts`	INTEGER,
	`draft_flags`	INTEGER,
	`draft_reply_to_msg_id`	INTEGER,
	`draft_message`	TEXT,
	`draft_entities`	TEXT,
	`draft_date`	INTEGER,
    PRIMARY KEY(`peer`)
);");
            Execute(database, "CREATE INDEX IF NOT EXISTS `Dialogs.peer_index` ON `Dialogs` (`peer`);");



            var version = SettingsHelper.DatabaseVersion;
            if (version < 7)
            {
                Execute(database, "ALTER TABLE `Chats` ADD COLUMN `admin_rights` INTEGER");
                Execute(database, "ALTER TABLE `Chats` ADD COLUMN `banned_rights` INTEGER");

                version = 7;
            }
            if (version < 8)
            {
                Execute(database, "ALTER TABLE `Chats` ADD COLUMN `index` INTEGER NOT NULL");
                Execute(database, "ALTER TABLE `Dialogs` ADD COLUMN `index` INTEGER NOT NULL");

                version = 8;
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
