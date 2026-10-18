"""Read public acquisition facts and cross-check Ornament row identities; review only."""
import datetime, hashlib, json, urllib.request
def get(url):
    req=urllib.request.Request(url,headers={"User-Agent":"SentinelHub-MetadataAudit/1.0 (https://github.com/MarshalTitan/SentinelHub)","Accept":"application/json"})
    with urllib.request.urlopen(req,timeout=90) as response: raw=response.read(4_000_001)
    if len(raw)>4_000_000: raise ValueError("Response too large")
    return json.loads(raw),hashlib.sha256(raw).hexdigest()
url="https://ffxivcollect.com/api/fashions?language=en"
body,digest=get(url)
if len(body["results"]) != body["count"] or not 20<=body["count"]<=200: raise ValueError("Incomplete fashions response")
rows=[]
for m in body["results"]:
    rows.append({"Id":m["id"],"EnglishName":m["name"],"Patch":m["patch"],"ItemId":m["item_id"],
        "Sources":[{"Type":s["type"],"Text":s["text"]} for s in m["sources"] if s["type"]!="Unknown" and s["text"]]})
snapshot={"Provider":"FFXIV Collect","Endpoint":url,"RetrievedUtc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "ResponseSha256":digest,"Permission":"https://ffxivcollect.com/ ; https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/","Accessories":sorted(rows,key=lambda r:r["Id"])}

game,gamehash=get("https://v2.xivapi.com/api/sheet/Ornament?fields=Singular,Icon,Model,Order&limit=500")
retired={22,25,26,32}
valid=[r for r in game["rows"] if r["row_id"] and r["row_id"] not in retired and r["fields"]["Singular"] and r["fields"]["Icon"]["id"] and r["fields"]["Model"] and r["fields"]["Order"]>=0]
by_id={r["row_id"]:r["fields"]["Singular"].casefold() for r in valid}
if len({r["Id"] for r in rows})!=len(rows) or set(by_id)!=set(r["Id"] for r in rows): raise ValueError("Catalog identity set mismatch")
if any(by_id[r["Id"]]!=r["EnglishName"].casefold() for r in rows): raise ValueError("Name mismatch")
if any(not isinstance(r["Id"],int) or not 0<r["Id"]<=65535 or len(r["EnglishName"])>200 or any(ord(ch)<32 for ch in r["EnglishName"]) for r in rows): raise ValueError("Invalid identity")
if any(len(s["Text"])>1000 or any(ord(ch)<32 for ch in s["Text"]) for r in rows for s in r["Sources"]): raise ValueError("Invalid source")
print(json.dumps({"accessories":len(rows),"with_sources":sum(bool(r["Sources"]) for r in rows),"matched_game_rows":len(valid),"retired_ids":sorted(retired),"game_response_sha256":gamehash,"game_version":game["version"],"source_response_sha256":digest}))
with open("fashion-source-review.json","w",encoding="utf-8") as f: json.dump(snapshot,f,ensure_ascii=True,indent=2)
