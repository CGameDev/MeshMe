<?php
declare(strict_types=1);
final class RateLimit {
    public static function consume(PDO $db,array $dimensions): void {
        $now=time();sort($dimensions,SORT_STRING);$blocked=false;$db->beginTransaction();
        try {
            foreach($dimensions as $dimension){
                $id=hash('sha256',$dimension);
                query($db,'INSERT IGNORE INTO rate_limits(dimension,attempts,window_utc,blocked_until_utc) VALUES(?,0,?,0)',[$id,$now]);
                $r=query($db,'SELECT * FROM rate_limits WHERE dimension=? FOR UPDATE',[$id])->fetch();
                $count=$now-(int)$r['window_utc']>=3600?1:(int)$r['attempts']+1;
                $until=$count>20?max((int)$r['blocked_until_utc'],$now+min(3600,2**min(12,$count-20))):0;
                $blocked=$blocked||$until>$now;
                query($db,'UPDATE rate_limits SET attempts=?,window_utc=?,blocked_until_utc=? WHERE dimension=?',[$count,$count===1?$now:$r['window_utc'],$until,$id]);
            }
            $db->commit();if($blocked)throw new ApiError('RATE_LIMITED',429);
        }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
    }
}
