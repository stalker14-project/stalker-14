### Localization for role ban command

cmd-roleban-desc = Блокує гравця від ролі
cmd-roleban-help = Використання: roleban <ім'я або айді користувача> <робота> <причина> [тривалість у хвилинах, не вказувати або 0 для постійної заборони] [тяжкість]

## Completion result hints
cmd-roleban-hint-1 = <ім'я або айді користувача>
cmd-roleban-hint-2 = <робота>
cmd-roleban-hint-3 = <причина>
cmd-roleban-hint-4 = [тривалість у хвилинах, не вказувати або 0 для постійної заборони]
cmd-roleban-hint-5 = [тяжкість]

cmd-roleban-hint-duration-1 = Пермаментно
cmd-roleban-hint-duration-2 = 1 день
cmd-roleban-hint-duration-3 = 3 дні
cmd-roleban-hint-duration-4 = 1 тиждень
cmd-roleban-hint-duration-5 = 2 тижні
cmd-roleban-hint-duration-6 = 1 місяць


### Localization for role unban command

cmd-roleunban-desc = Скасовує блокування ролі для гравця
cmd-roleunban-help = Використання: roleunban <айді роль бану>

## Completion result hints
cmd-roleunban-hint-1 = <айді роль бану>


### Localization for roleban list command

cmd-rolebanlist-desc = Показує список роль-банів користувача
cmd-rolebanlist-help = Використання: <ім'я або айді користувача> [включати unbanned]

## Completion result hints
cmd-rolebanlist-hint-1 = <ім'я або айді користувача>
cmd-rolebanlist-hint-2 = [включати unbanned]


cmd-roleban-minutes-parse = {$time} не є дійсною кількістю хвилин.\n{$help}
cmd-roleban-severity-parse = ${severity} не є допустимим ступенем тяжкості\n{$help}.
cmd-roleban-arg-count = Неправильна кількість аргументів.
cmd-roleban-job-parse = Робота {$job} не існує.
cmd-roleban-name-parse = Не вдалося знайти гравця з таким ім'ям.
cmd-roleban-existing = {$target} вже має бан роль для {$role}.
cmd-roleban-success = Роль забенено для {$target} з {$role} з причиною {$reason} {$length}.

cmd-roleban-inf = Пермаментно
cmd-roleban-until =  до {$expires}

# Department bans
cmd-departmentban-desc = Банить гравця з ролей входячих в департамент
cmd-departmentban-help = Використання: departmentban <ім'я або айді користувача> <департамент> <причина> [тривалість у хвилинах, не вказувати або 0 для постійної заборони]
