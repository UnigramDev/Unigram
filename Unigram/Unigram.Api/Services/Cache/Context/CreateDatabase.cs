using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.Services.Cache.Context
{
    internal static class CreateDatabase
    {
        public static readonly string Query =
@"
CREATE TABLE IF NOT EXISTS `Users` (
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
	`photo_large_local_id`	INTEGER,
	`photo_large_secret`	BIGINT,
	`photo_large_volume_id`	BIGINT,
	`photo_large_dc_id`	INTEGER,
	`status`	INTEGER,
	`status_was_online`	INTEGER,
	`status_expires`	INTEGER,
	PRIMARY KEY(`id`)
);
CREATE INDEX IF NOT EXISTS `Users.username_index` ON `Users` (`username`);
CREATE INDEX IF NOT EXISTS `Users.phone_index` ON `Users` (`phone`);
CREATE INDEX IF NOT EXISTS `Users.last_name_index` ON `Users` (`last_name`);
CREATE INDEX IF NOT EXISTS `Users.first_name_index` ON `Users` (`first_name`);";
    }
}
